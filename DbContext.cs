using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using Westwind.Utilities;

namespace GlobalLib.Database
{
    public class DbContext
    {
        private readonly DbProviderFactory _provider;
        private readonly string _connectionString;
        private readonly string _name;
        protected IDbConnection _connection;


        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
        private readonly LinkedList<UnitOfWork> _workItems = new LinkedList<UnitOfWork>();

        protected UnitOfWork _transaction;

        public DbContext(string connectionStringName)
        {
            // iniciar a configuração da ligação à base de dados
            // usamos a o providerfactories para que a ligação tenha o provider de acordo com a connection string

            if (connectionStringName == null) throw new ArgumentNullException(nameof(connectionStringName));

            var conStr = ConfigurationManager.ConnectionStrings[connectionStringName];
            if (conStr == null)
                throw new ConfigurationErrorsException($"Falta connection string '{connectionStringName}' em app.config ou web.config.");

            _name = conStr.ProviderName;

#if !NETSTANDARD2_0
            _provider = DbProviderFactories.GetFactory(conStr.ProviderName);
#else
            _provider = DataUtils.GetDbProviderFactory(conStr.ProviderName);
#endif
            _connectionString = conStr.ConnectionString;
        }

        /// <summary>
        /// Ensures that a connection is ready for querying or creating transactions
        /// </summary>
        /// <remarks></remarks>
        public IDbConnection CreateOrReuseConnection()
        {
            if (_connection != null) return _connection;

            _connection = _provider.CreateConnection();
            if (_connection == null)
                throw new ConfigurationErrorsException($"Falha na criação da ligação à bd utilizando a connection string '{_connectionString}'");

            _connection.ConnectionString = _connectionString;
            return _connection;
        }

        /// <summary>
        /// Creates a new <see cref="DbUnitOfWork"/>.
        /// </summary>
        /// <param name="isolationLevel">The <see cref="IsolationLevel"/> used for the transaction inside this unit of work. Default value: <see cref="IsolationLevel.ReadCommitted"/></param>
        /// <returns></returns>
        public UnitOfWork CreateUnitOfWork(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            CreateOrReuseConnection();

            //To create a transaction, our connection needs to be open.
            //If we need to open the connection ourselves, we're also in charge of closing it when this transaction commits or rolls back.
            //This will be done by RemoveTransactionAndCloseConnection in that case.
            bool wasClosed = _connection.State == ConnectionState.Closed;
            if (wasClosed) _connection.Open();

            try
            {
                UnitOfWork unit;
                IDbTransaction transaction = _connection.BeginTransaction(isolationLevel);

                if (wasClosed)
                    unit = new UnitOfWork(transaction, RemoveTransactionAndCloseConnection, RemoveTransactionAndCloseConnection);
                else
                    unit = new UnitOfWork(transaction, RemoveTransaction, RemoveTransaction);

                _rwLock.EnterWriteLock();
                _workItems.AddLast(unit);
                _rwLock.ExitWriteLock();

                return unit;
            }
            catch
            {
                //Close the connection if we're managing it, and if an exception is thrown when creating the transaction.
                if (wasClosed) _connection.Close();

                throw; //Rethrow the original transaction
            }
        }

        public IDbTransaction GetCurrentTransaction()
        {
            IDbTransaction currentTransaction = null;
            _rwLock.EnterReadLock();
            if (_workItems.Any()) currentTransaction = _workItems.First.Value.Transaction;
            _rwLock.ExitReadLock();

            return currentTransaction;
        }

        public void RemoveTransaction(UnitOfWork workItem)
        {
            _rwLock.EnterWriteLock();
            _workItems.Remove(workItem);
            _rwLock.ExitWriteLock();
        }

        public void RemoveTransactionAndCloseConnection(UnitOfWork workItem)
        {
            _rwLock.EnterWriteLock();
            _workItems.Remove(workItem);
            _rwLock.ExitWriteLock();

            _connection.Close();
        }

        /// <summary>
        /// Implements <see cref="IDisposable.Dispose"/>.
        /// </summary>
        public void Dispose()
        {
            //Use an upgradeable lock, because when we dispose a unit of work,
            //one of the removal methods will be called (which enters a write lock)
            _rwLock.EnterUpgradeableReadLock();
            try
            {
                while (_workItems.Any())
                {
                    var workItem = _workItems.First;
                    workItem.Value.Dispose(); //rollback, will remove the item from the LinkedList because it calls either RemoveTransaction or RemoveTransactionAndCloseConnection
                }
            }
            finally
            {
                _rwLock.ExitUpgradeableReadLock();
            }

            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
    }
}

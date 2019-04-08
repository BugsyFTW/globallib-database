using Dapper;
using Dapper.Contrib.Extensions;
using GlobalLib.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using AutoMapper;
using System.Dynamic;

namespace GlobalLib.Database
{
    public class DbTable<T> where T : class
    {
        protected DbContext dbConnection;

        public int UserOfApplicationID = -1;

        protected string _TableName;
        protected string _KeyName;

        public T Model;

        public DbTable(DbContext connectionContext, int UserAppID = -1)
        {
            dbConnection = connectionContext;

            InitClass(UserAppID);
        }

        public DbTable(string connectionStringName, int UserAppID = -1)
        {
            dbConnection = new DbContext(connectionStringName);
            InitClass(UserAppID);
        }

        protected void InitClass(int UserAppID = -1)
        {

            // **** HACK **** 
            // Até se arranjar melhor método de detectar se o AutoMapper 
            // já está inicializado tentamos inicializar
            // Se der excepção é porque já está inicilizada
            // Captura-se a excepção e continamos a execução
            try
            {
                Mapper.Initialize(cfg => { });
            }
            catch (Exception)
            {
                // ok, já tinha sido inicializado.
                // ignoramos o erro e avançamos.
            }
           
            // restantes configurações
            // recebe o utilizador em activo da aplicação
            UserOfApplicationID = UserAppID;

            // se temos um tipo objecto genérico não vamos procurar nada
            if((typeof(T).Name.ToLowerInvariant() != typeof(object).Name.ToLowerInvariant()))
            {
                // criamos uma instancia  do objecto para poder consultar as propriedades
                Model = (T)Activator.CreateInstance(typeof(T), new object[] { });

                // Atributos da classe POCO

                // Nome da tabela
                TableAttribute tableAttributes = (TableAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(TableAttribute));

                if(tableAttributes != null)
                {
                    _TableName = tableAttributes.Name;
                }
                

                // Campo Chave
                var props = typeof(T).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(Dapper.Contrib.Extensions.KeyAttribute))).FirstOrDefault();

                if(props != null)
                {
                    _KeyName = props.Name;
                }
            }



        }

        public virtual T Insert(T item, out long newItemID, IDbTransaction activeTransaction = null)
        {
            // campos audit
            if (UserOfApplicationID >= 0)
            {
                item.GetType().GetProperty("InsertUserID").SetValue(item, UserOfApplicationID, null);
                item.GetType().GetProperty("InsertDate").SetValue(item, DateTime.Now.GetByTimeZone(), null);

                item.GetType().GetProperty("UpdateUserID").SetValue(item, null, null);
                item.GetType().GetProperty("UpdateDate").SetValue(item, null, null);
            }

             long itemId = dbConnection.CreateOrReuseConnection().Insert<T>(item, activeTransaction);

            newItemID = itemId;

            return this.Get(itemId, activeTransaction);
        }

        public virtual long Insert(IEnumerable<T> items, IDbTransaction activeTransaction = null)
        {
            // campos audit
            if (UserOfApplicationID >= 0)
            {
                foreach (T item in items)
                {
                    item.GetType().GetProperty("InsertUserID").SetValue(item, UserOfApplicationID, null);
                    item.GetType().GetProperty("InsertDate").SetValue(item, DateTime.Now.GetByTimeZone(), null);

                    item.GetType().GetProperty("UpdateUserID").SetValue(item, null, null);
                    item.GetType().GetProperty("UpdateDate").SetValue(item, null, null);
                }
            }

            long itemId = dbConnection.CreateOrReuseConnection().Insert(items, activeTransaction);;

            return itemId;
        }

        public virtual T Update(T item, IDbTransaction activeTransaction = null)
        {
            // descobrir campo chave e saber valor
            Type t = item.GetType();
            var props = t.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            var value = props.First<PropertyInfo>().GetValue(item, null);


            // campos de audit
            if (UserOfApplicationID >= 0)
            {
                item.GetType().GetProperty("InsertUserID").SetValue(item, item.GetType().GetProperty("InsertUserID").GetValue(item, null));
                item.GetType().GetProperty("InsertDate").SetValue(item, item.GetType().GetProperty("InsertDate").GetValue(item, null));

                item.GetType().GetProperty("UpdateUserID").SetValue(item, UserOfApplicationID, null);
                item.GetType().GetProperty("UpdateDate").SetValue(item, DateTime.Now.GetByTimeZone(), null);
            }

            bool updateSucess = dbConnection.CreateOrReuseConnection().Update<T>(item, activeTransaction);
            if(!updateSucess)
            {
                return null;
            }
            else
            {
                return this.Get(Convert.ToInt64(value), activeTransaction);
            }
        
        }

        public virtual void Delete(T item, IDbTransaction activeTransaction = null)
        {
            dbConnection.CreateOrReuseConnection().Delete<T>(item, activeTransaction);
        }


        public virtual T Get(long id, IDbTransaction activeTransaction = null)
        {
            return dbConnection.CreateOrReuseConnection().Get<T>(id, activeTransaction);
        }

        public virtual T Get(int id, IDbTransaction activeTransaction = null)
        {
            return dbConnection.CreateOrReuseConnection().Get<T>(id, activeTransaction);
        }

        public virtual IEnumerable<T> GetAll(IDbTransaction activeTransaction = null)
        {
            return dbConnection.CreateOrReuseConnection().GetAll<T>(activeTransaction);
        }

        private  void _AddWhereParameter(ref List<WhereParameter> whereParams, string paramFieldName, string paramName, object paramValue, string logicOperator = "=")
        {
            if (whereParams == null)
            {
                whereParams = new List<WhereParameter>();
            }

            whereParams.Add(new WhereParameter
            {
                FieldName = paramFieldName ?? paramName,
                ParamName = paramName ?? paramFieldName,
                LogicOperator = logicOperator,
                Value = paramValue
            });
        }


        public void AddWhereParameter(ref List<WhereParameter> whereParams, string paramName, object paramValue, string logicOperator = "=")
        {
            this._AddWhereParameter(ref whereParams, null, paramName, paramValue, logicOperator);
        }

        public void AddWhereParameter(ref List<WhereParameter> whereParams, string paramFieldName, string paramName, object paramValue, string logicOperator = "=")
        {
            this._AddWhereParameter(ref whereParams, paramFieldName, paramName, paramValue, logicOperator);
        }


        /// <summary>
        /// Constroi o paramentros para o SqlBuilder do Dapper
        /// </summary>
        /// <param name="whereParams"></param>
        /// <param name="sqlBuild"></param>
        /// <returns></returns>
        public DynamicParameters BuildWhere(ref List<WhereParameter> whereParams, SqlBuilder sqlBuild)
        {
            // se queremos fazer restrições
            DynamicParameters dbArgs = null;
            if (whereParams != null)
            {
                // converte para parametros do dapper
                dbArgs = new DynamicParameters();

                // parametros do where
                foreach (var p in whereParams)
                {

                    if ( (p.Value.ToString().IndexOf('%') >= 0) || (p.Value.ToString().IndexOf('*') >= 0) )
                    {
                        sqlBuild.Where($"{p.FieldName} like @{p.ParamName.Replace(".", "")}");
                    }
                    else
                    {
                        sqlBuild.Where($"{p.FieldName} {p.LogicOperator} @{p.ParamName.Replace(".", "")}");
                    }


                    dbArgs.Add(p.ParamName.Replace(".", ""), p.Value);

                }
            }

            return dbArgs;
        }

        public string GetOrderForQuery(List<OrderByField> orderFieldList, string tableName = null)
        {
            string orderBy = "";
            foreach (var item in orderFieldList)
            {
                if (orderBy != "")
                {
                    orderBy += ",";
                }

                string tableNameOrderBy = "";

                if ( (tableName != null) && (tableName.Trim()!="") )
                {
                    tableNameOrderBy = tableName.Trim() + ".";
                }
                orderBy += $"{tableNameOrderBy}{item.FieldName} {item.SortOrder}".Trim();
            }
            return orderBy;
        }

        /// <summary>
        /// Executa a query, tendo em conta o template SQL e restantes parametros
        /// </summary>
        /// <typeparam name="TAnyClass"></typeparam>
        /// <param name="sqlTemplate"></param>
        /// <param name="whereParams"></param>
        /// <param name="activePagination"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="activeTransaction"></param>
        /// <returns></returns>
        private Tuple<PagedResults<TAnyClass>, IEnumerable<dynamic>> ExecuteQuery<TAnyClass>(string sqlTemplate, string keyFieldName, List<OrderByField> orderByList=null, List<WhereParameter> whereParams=null, bool activePagination = false, int pageSize = 25, int pageNumber = 1, IDbTransaction activeTransaction = null)
        {
            var builder = new SqlBuilder();

            var getAllBuilder = builder.AddTemplate(sqlTemplate);

            // se queremos fazer restrições
            DynamicParameters dbArgs = BuildWhere(ref whereParams, builder);

            if((orderByList!=null) && (orderByList.Count>0))
            {
                builder.OrderBy(GetOrderForQuery(orderByList));
            }
            else
            {
                builder.OrderBy(keyFieldName);
            }

            string finalSqlClause = getAllBuilder.RawSql;

            var ret = dbConnection.CreateOrReuseConnection().Query(finalSqlClause, dbArgs, activeTransaction);

            PagedResults<TAnyClass> returnVal = new PagedResults<TAnyClass>();

            // se estamos a trabalhar com paginação, 
            // temos de preencher os dados sobre a paginação
            if(activePagination)
            { 
                returnVal.PageInfo.CurrentPage = pageNumber;
                returnVal.PageInfo.PageSize = pageSize;
                if(!ret.IsNullOrEmpty())
                {
                    returnVal.PageInfo.CurrentPageSize = ret.Count();
                
                    returnVal.PageInfo.TotalRecords = ret.FirstOrDefault().TotalNumberOfRecords;
                }
            }


            return Tuple.Create(returnVal, ret);

        }


        /// <summary>
        /// Retorna todos os registos da tabela indicada pelo modelo POCO de T
        /// Possibilita a filtragem
        /// </summary>
        /// <param name="whereParams"></param>
        /// <param name="activeTransaction"></param>
        /// <returns></returns>
        public virtual IEnumerable<T> GetAll(List<WhereParameter> whereParams = null, IDbTransaction activeTransaction = null)
        {
            if ((_TableName == null) || (_TableName.Trim() == ""))
            {
                throw new ArgumentException("Falta nome de Tabela");
            }

            var builder = new SqlBuilder();
            var getAllBuilder = builder.AddTemplate($"set language portuguese; SELECT * from {_TableName} /**where**/ ");

            // se queremos fazer restrições
            DynamicParameters dbArgs = BuildWhere(ref whereParams, builder);

            string finalSqlClause = getAllBuilder.RawSql;

            return dbConnection.CreateOrReuseConnection().Query<T>(finalSqlClause, dbArgs, activeTransaction);
        }


        /// <summary>
        /// Retorna os registos baseado numa query e modelos custom diferentes de T
        /// Possibilita a filtragem
        /// </summary>
        /// <typeparam name="TModeloQuery"></typeparam>
        /// <param name="query"></param>
        /// <param name="whereParams"></param>
        /// <param name="activeTransaction"></param>
        /// <returns></returns>
        public virtual IEnumerable<TModeloQuery> CustomQuery<TModeloQuery>(string query, string keyFieldName, List<OrderByField> orderByList, List<WhereParameter> whereParams = null, IDbTransaction activeTransaction = null)
        {
            string sqlTemplate = $@"set language portuguese;WITH dadosquery as (
                                        {query}
                                      )
                                      SELECT dadosquery.* from dadosquery /**where**/";

            var executeTuple = ExecuteQuery<TModeloQuery>(sqlTemplate, keyFieldName, orderByList, whereParams, false, 0, 0, activeTransaction);

            IEnumerable<TModeloQuery> returnVal = Mapper.Map<IEnumerable<TModeloQuery>>(executeTuple.Item2);

            return returnVal;

        }

        /// <summary>
        /// Retorna os registos da tabela indicada pelo modelo POCO de T, mas paginados.
        /// Possibilita a filtragem
        /// Só para SQLServer 2012 +
        /// </summary>
        /// <param name="pageSize">mr de registos por pagina</param>
        /// <param name="pageNumber">nº de pagina</param>
        /// <param name="whereParams"></param>
        /// <param name="activeTransaction"></param>
        /// <returns></returns>
        public virtual PagedResults<T> GetPagedResults(int pageSize, int pageNumber, List<OrderByField> orderBy = null, List<WhereParameter> whereParams = null, IDbTransaction activeTransaction = null)
        {

            if ((_TableName == null) || (_TableName.Trim() == ""))
            {
                throw new ArgumentException("Falta nome de Tabela");
            }


            if ((_KeyName == null) || (_KeyName.Trim() == ""))
            {
                throw new ArgumentException("Falta nome da Chave Primária");
            }

            string sqlTemplate = $@"set language portuguese;WITH pg AS
                                     (
                                          SELECT {_KeyName}
                                          ,      COUNT(*) OVER () as TotalNumberOfRecords
                                          FROM {_TableName} /**where**/
                                           /**orderby**/
                                          OFFSET {pageSize} * ({pageNumber} - 1) ROWS
                                          FETCH NEXT {pageSize} ROWS ONLY
                                      )
                                      SELECT pg.TotalNumberOfRecords,  t.* from {_TableName} as T
                                      INNER JOIN PG ON pg.{_KeyName} = t.{_KeyName} /**orderby**/";

            var executeTuple = ExecuteQuery<T>(sqlTemplate, _KeyName, orderBy, whereParams, true, pageSize, pageNumber, activeTransaction);

            PagedResults<T> returnVal = executeTuple.Item1;

            returnVal.Records = Mapper.Map<IEnumerable<T>>(executeTuple.Item2);

            return returnVal;

        }

        /// <summary>
        /// Retorna os registos baseado numa query e modelos custom diferentes de T (ou mesmo T), mas paginados.
        /// Possibilita a filtragem
        /// Só para SQLServer 2012 +
        /// </summary>
        /// <typeparam name="TModeloQuery"></typeparam>
        /// <param name="query"></param>
        /// <param name="keyFieldName"></param>
        /// <param name="pageSize"></param>
        /// <param name="pageNumber"></param>
        /// <param name="whereParams"></param>
        /// <param name="activeTransaction"></param>
        /// <returns></returns>
        public virtual PagedResults<TModeloQuery> CustomQueryPaged<TModeloQuery>(string query, string keyFieldName, List<OrderByField> orderByList,  int pageSize, int pageNumber, List<WhereParameter> whereParams = null, IDbTransaction activeTransaction = null) 
        {
            string sqlTemplate = $@"set language portuguese;WITH dadosquery as (
                                        {query}
                                        
                                      ),
                                     pg AS
                                     (
                                          SELECT {keyFieldName}
                                          ,      COUNT(*) OVER () as TotalNumberOfRecords
                                          FROM dadosquery /**where**/
                                           /**orderby**/
                                          OFFSET {pageSize} * ({pageNumber} - 1) ROWS
                                          FETCH NEXT {pageSize} ROWS ONLY
                                      )
                                      SELECT pg.TotalNumberOfRecords,  dadosquery.* from dadosquery
                                      INNER JOIN PG ON pg.{keyFieldName} = dadosquery.{keyFieldName} /**orderby**/";

            var executeTuple = ExecuteQuery<TModeloQuery>(sqlTemplate, keyFieldName, orderByList, whereParams, true, pageSize, pageNumber, activeTransaction);

            PagedResults<TModeloQuery> returnVal = executeTuple.Item1;

            returnVal.Records = Mapper.Map<IEnumerable<TModeloQuery>>(executeTuple.Item2);

            return returnVal;

        }

        /// <summary>
        /// Retorna o número de registos da tabela
        /// </summary>
        /// <returns></returns>
        public int GetCount(IDbTransaction activeTransaction = null)
        {

            if ((_TableName == null) || (_TableName.Trim() == ""))
            {
                throw new ArgumentException("Falta nome de Tabela");
            }


            var item = dbConnection.CreateOrReuseConnection().Query($"set language portuguese;select count(*) contagem from {_TableName}", activeTransaction).FirstOrDefault();

            return item.contagem;
        }


        /// <summary>
        /// Retorna o número de registos da query
        /// </summary>
        /// <returns></returns>
        public int GetCount(string sqlQuery,  IDbTransaction activeTransaction = null)
        {
            var item = dbConnection.CreateOrReuseConnection().Query($"set language portuguese;select count(*) contagem from ({sqlQuery})", activeTransaction).FirstOrDefault();

            return item.contagem;
        }


        /// <summary>
        /// Retorna o registo com o valor da chave primária.
        /// Se não encontra, devolve o registo por defeito
        /// </summary>
        /// <param name="PrimaryKeyValue"></param>
        /// <param name="DefaultFlagFieldName"></param>
        /// <returns></returns>
        public virtual T GetByKeyOrDefault(int PrimaryKeyValue, string DefaultFlagFieldName = "PredefinidoSN", IDbTransaction activeTransaction = null)
        {

            var item = dbConnection.CreateOrReuseConnection().Get<T>(PrimaryKeyValue, activeTransaction);

            if(item != null)
            {
                return item;
            }
            else
            {
                return dbConnection.CreateOrReuseConnection().Query<T>($"set language portuguese;select * from {_TableName} where {DefaultFlagFieldName} = 1", activeTransaction).FirstOrDefault();
            }

        }

        /// <summary>
        /// Validação baseada em modelo
        /// </summary>
        /// <param name="item"></param>
        /// <param name="BusinessRulesErrors"></param>
        /// <returns></returns>
        public virtual bool IsDomainValid(ref T item, out List<ValidationError> BusinessRulesErrors)
        {
            BusinessRulesErrors = null;
            return true;
        }

    }
}

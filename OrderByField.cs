using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLib.Database
{
    /// <summary>
    /// Campos de ordenação e ordem de ordenação
    /// Serve para traduzir os pedidos de ordenção em instruções validas de ordenação.
    /// </summary>
    public class OrderByField
    {
        private string _FieldName;
        private string _SortOrder;

        /// <summary>
        /// Dado o campo da querystring retira a parte do campo e o operador de ordenação
        /// </summary>
        public string FieldName
        {
            get
            {
                return _FieldName;
            }
            set
            {
                // separa a ordem do nome do campo
                //   asc
                // + asc
                // - asc
                string tempVal = value.Trim().ToLowerInvariant();

                switch (tempVal.Substring(0, 1))
                {
                    case "-":
                        {
                            _FieldName = tempVal.Substring(1, tempVal.Length - 1);
                            _SortOrder = "DESC";
                            break;
                        }
                    default:
                        {
                            if (tempVal.StartsWith("+"))
                            {
                                _FieldName = tempVal.Substring(1, tempVal.Length - 1);
                            }
                            else
                            {
                                _FieldName = tempVal;
                            }

                            _SortOrder = "ASC";
                            break;
                        }

                }
            }
        }

        /// <summary>
        /// Indica a ordem de ordenação
        /// </summary>
        public string SortOrder
        {
            get
            {
                return _SortOrder;
            }
        }
    }
}

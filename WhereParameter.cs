using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLib.Database
{
    public class WhereParameter
    {
        private string _ParamName = null;

        public string ParamName
        {
            set
            {
                this._ParamName = value;
            }
            get
            {
                return this._ParamName ?? this.FieldName;
            }
        }


        private string _FieldName = null;
        public string FieldName { 
            set {
                this._FieldName = value;
            }
            get
            {
                return this._FieldName ?? this.ParamName;
            }
        }

        public string  LogicOperator{ get; set; }
        public object Value { get; set; }

        public WhereParameter()
        {
            this._FieldName = null;
            // operador de comparação por defeito do where
            LogicOperator = "=";
        }
    }
}

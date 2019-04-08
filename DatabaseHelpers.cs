using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLib.Database
{
    public static class DatabaseHelpers
    {
        public static int GetKeyValueOf<T>(T item) where T : class
        {
            Type t = item.GetType();
            var props = t.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)));
            var value = props.First<PropertyInfo>().GetValue(item, null);

            return Convert.ToInt32(value);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLib.Database
{
    public class PagedResults<T>
    {
        public DataPageInfo PageInfo { get; set; }
        public IEnumerable<T> Records { get; set; }

        public PagedResults()
        {
            PageInfo = new DataPageInfo();

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GlobalLib.Database
{
    public class DataPageInfo
    {
        public Int64 TotalRecords { get; set; }
        public int PageSize { get; set; }

        public int TotalNrPages
        {
            get
            {
                return (int)Math.Ceiling((double)TotalRecords / PageSize);
            }

        }
        public int CurrentPage { get; set; }

        public int CurrentPageSize { get; set; }
    }
}

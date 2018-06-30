using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatusAggregator
{
    public interface ICursor
    {
        DateTime Get();
        Task Set(DateTime value);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Validation.ContentScan.Core
{
    public class CheckContentScanStatusData
    {
        public CheckContentScanStatusData(
           Guid validationSetId)
        {
            ValidationSetId = validationSetId;
        }

        public Guid ValidationSetId { get; }
    }
}

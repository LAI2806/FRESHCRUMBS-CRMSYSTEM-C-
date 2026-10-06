using System;
using System.Collections.Generic;
using System.Text;

namespace freshcrumbs.CRM.winforms.Models
{
    public class CompanyModel
    {
        public List<string>? EnabledFeatures { get; set; }

        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;
    }
}
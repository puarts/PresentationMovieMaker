using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.DataModels
{
    public class ApplicationSetting
    {
        public string MovieSettingPath { get; set; } = string.Empty;

        public string AzureServiceRegion { get; set; } = string.Empty;

        public string AzureSubscriptionKey { get; set; } = string.Empty;

        public string AudioApplausePath { get; set; } = string.Empty;
        public string BouyomiChanPath { get; set; } = string.Empty;
        public string BouyomiChanRemoteTalkPath { get; set; } = string.Empty;
    }
}

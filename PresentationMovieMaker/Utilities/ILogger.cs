using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresentationMovieMaker.Utilities
{
    public interface ILogger
    {
        void WriteLogLine(string message);
        void WriteErrorLogLine(string message, Exception exception);
        void WriteErrorLogLine(string message);
    }
}

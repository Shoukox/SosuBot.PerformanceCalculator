using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.PerformanceCalculator
{
    public record PPCalculationResult
    {
        public required double Pp { get; set; }
        public required double CalculatedAccuracy { get; set; }
    }
}

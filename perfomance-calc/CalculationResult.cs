using osu.Game.Rulesets.Scoring;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace perfomance_calc
{
    public record CalculationResult
    {
        public required double PPValue { get; set; }
        public required double Accuracy { get; set; }
    }
}

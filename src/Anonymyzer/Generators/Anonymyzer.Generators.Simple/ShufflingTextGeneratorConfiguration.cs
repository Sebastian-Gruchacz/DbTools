namespace Anonymyzer.Generators.Simple
{
    public class ShufflingTextGeneratorConfiguration
    {
        /// <summary>
        /// How many shuffling iterations will be performed in comparison to original string length
        /// </summary>
        public decimal IterationsMultiplier { get; set; }

        /// <summary>
        /// Minimum string length to apply shuffling
        /// </summary>
        public int MinimumLengthToApply { get; set; }
    }
}
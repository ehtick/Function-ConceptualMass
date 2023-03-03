namespace ConceptualMassFromModules
{
    public static class Logging
    {
        private static ConceptualMassFromModulesOutputs _outputs = null;
        public static void SetLogTarget(ConceptualMassFromModulesOutputs output)
        {
            _outputs = output;
        }
        public static void LogWarning(string message)
        {
            _outputs?.Warnings.Add(message);
        }
    }
}
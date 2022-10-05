namespace CreateEnvelopes
{
    public static class Logging
    {
        private static CreateEnvelopesOutputs _outputs = null;
        public static void SetLogTarget(CreateEnvelopesOutputs output)
        {
            _outputs = output;
        }
        public static void LogWarning(string message)
        {
            _outputs?.Warnings.Add(message);
        }
    }
}
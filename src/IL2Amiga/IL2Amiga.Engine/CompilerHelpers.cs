using System.Diagnostics;

namespace IL2Amiga.Engine
{
    public static class CompilerHelpers
    {
#pragma warning disable CA1710 // Identifiers should have correct suffix
        public static event Action<string>? DebugEvent;
#pragma warning restore CA1710 // Identifiers should have correct suffix

        private static void DoDebug(string message)
        {
            if (DebugEvent is not null)
            {
                DebugEvent(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }

        [Conditional("COSMOSDEBUG")]
        public static void Debug(string message, params object[] @params)
        {
            if (@params is not null)
            {
                message = $"{message} : ";
                for (int i = 0; i < @params.Length; i++)
                {
                    var xParam = @params[i].ToString();
                    if (!string.IsNullOrWhiteSpace(xParam))
                    {
                        message = $"{message} {xParam}";
                    }
                }
            }
            DoDebug(message);
        }

        [Conditional("COSMOSDEBUG")]
        public static void Debug(string aMessage) => DoDebug(aMessage);
    }
}

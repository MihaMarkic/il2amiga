namespace IL2Amiga.Engine
{
    public static class CompilerHelpers
    {
        public static string FormatMessage(string message, params object[] @params)
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
            return message;
        }
    }
}

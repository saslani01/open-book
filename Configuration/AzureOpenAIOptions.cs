namespace OpenBook.Configuration
{
    public class AzureOpenAIOptions
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = "gpt-4-mini";
    }
}
namespace DemoAzureDurableFaaS
{
    public class HelloDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Message { get; set; }
    }

    public class InputDto
    {
        public string CityName { get; set; }
    }
}
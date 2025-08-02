using Generators.Attributes;

var example = new Example();
example.SayHello();
example.HelloWorld();

var person = new Person
{
    Name = "John",
    LastName = "Doe",
    Email = "john@example.com",
    Age = 30,
    IsActive = true
};

var stringValues = person.GetAllStringValues();
Console.WriteLine($"String values: {string.Join(", ", stringValues)}");

var stats = new Statistics
{
    Count = 100,
    Total = 500,
    Average = 5,
    Name = "Test",
    IsValid = true
};

var intValues = stats.GetAllIntValues();
Console.WriteLine($"Int values: {string.Join(", ", intValues)}");

[GenerateHelloWorld]
public partial class Example
{
    [GenerateBody]
    public partial void SayHello();
}

[GeneratePropertyValuesList(typeof(string))]
public partial class Person
{
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

[GeneratePropertyValuesList(typeof(int))]
public partial class Statistics
{
    public int Count { get; set; }
    public int Total { get; set; }
    public int Average { get; set; }
    public string? Name { get; set; }
    public bool IsValid { get; set; }
}
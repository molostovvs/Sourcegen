using App;

using I18N;


HelloWorldDemo();

PropertyExtractionDemo();

I18NDemo();

// Regex example

// Logging example

static void HelloWorldDemo()
{
    var example = new Example();

    example.SayHello();
    example.HelloWorld();
}

static void PropertyExtractionDemo()
{
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
}

static void I18NDemo()
{
    var amazingEn = SomeNamespaceI18N.AmazingKey.Render("en-US");
    var amazingRu = SomeNamespaceI18N.AmazingKey.Render("ru-RU");

    Console.WriteLine($"AmazingKey (EN): {amazingEn}");
    Console.WriteLine($"AmazingKey (RU): {amazingRu}");
    Console.WriteLine();

    var complexEn = SomeNamespaceI18N.ComplexKey
        .WithName("John Doe")
        .WithBalance("$1,234.56")
        .WithReason("insufficient funds")
        .Render("en-US");

    var complexRu = SomeNamespaceI18N.ComplexKey
        .WithName("Иван Иванов")
        .WithBalance("₽1,234.56")
        .WithReason("недостаточно средств")
        .Render("ru-RU");

    Console.WriteLine($"ComplexKey (EN): {complexEn}");
    Console.WriteLine($"ComplexKey (RU): {complexRu}");
    Console.WriteLine();
}

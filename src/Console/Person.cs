using Generators.Attributes;

namespace App;

[GeneratePropertyValuesList(typeof(string))]
public partial class Person
{
    public string? Name { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

using Generators.Attributes;

namespace App;

[GeneratePropertyValuesList(typeof(int))]
public partial class Statistics
{
    public int Count { get; set; }
    public int Total { get; set; }
    public int Average { get; set; }
    public string? Name { get; set; }
    public bool IsValid { get; set; }
}

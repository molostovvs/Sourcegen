using Generators.Attributes;

namespace App;

[GenerateMethod]
public partial class Example
{
    [GenerateBody]
    public partial void SayHello();
}

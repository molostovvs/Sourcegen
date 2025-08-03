using Generators.Attributes;

var example = new Example();
example.HelloWorld();

[GenerateHelloWorld]
public partial class Example
{
}
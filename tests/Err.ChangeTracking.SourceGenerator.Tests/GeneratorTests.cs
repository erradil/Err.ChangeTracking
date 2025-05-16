using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Err.ChangeTracking.SourceGenerator.Tests;

public class GeneratorTests
{
    private const string PersonClassText = """
                                           namespace Err.ChangeTracking.SampleDemo.Models;

                                           [Trackable]
                                           internal partial record Person
                                           {
                                               public partial string Name { get; set; }

                                               [Trackable]
                                               public partial struct Address
                                               {
                                                   public partial string Street { get; set; }
                                               }
                                           }
                                           """;

    // Minimal Trackable attribute implementation for testing
    private const string ChangeTrackingText = """
                                              namespace Err.ChangeTracking
                                              {
                                                  [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
                                                  public class TrackableAttribute : System.Attribute
                                                  {
                                                  }

                                                  public interface ITrackable<T>
                                                  {
                                                      IChangeTracking<T> GetChangeTracker();
                                                  }

                                                  public interface IChangeTracking<T>
                                                  {
                                                      void RecordChange(string propertyName, object? oldValue, object? newValue);
                                                  }

                                                  public class ChangeTracking<T> : IChangeTracking<T>
                                                  {
                                                      public ChangeTracking(T entity) { }
                                                      public void RecordChange(string propertyName, object? oldValue, object? newValue) { }
                                                  }
                                              }
                                              """;

    [Fact]
    public void GeneratePersonRecord()
    {
        // Create an instance of the source generator
        var generator = new ChangeTrackingGenerator();

        // Use GeneratorDriver for testing
        var driver = CSharpGeneratorDriver.Create(generator);

        // Create a compilation with the required source code and references
        var compilation = CreateCompilation();

        // Run the generator
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // Verify Person.g.cs was generated
        var PersonGeneratedTree = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("Err.ChangeTracking.SampleDemo.Models.Person.g.cs"));

        Assert.NotNull(PersonGeneratedTree);
        //Assert.Equal(ExpectedPersonGeneratedText, PersonGeneratedTree.GetText().ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);

        // Verify nested Address struct was properly generated
        var addressGeneratedTree = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("Err.ChangeTracking.SampleDemo.Models.Person.Address.g.cs"));

        Assert.NotNull(addressGeneratedTree);
        //Assert.Equal(ExpectedAddressGeneratedText, addressGeneratedTree.GetText().ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences:true);
    }

    private static Compilation CreateCompilation()
    {
        return CSharpCompilation.Create(nameof(GeneratorTests),
            [
                CSharpSyntaxTree.ParseText(PersonClassText),
                CSharpSyntaxTree.ParseText(ChangeTrackingText)
            ],
            [
                // References required for compilation
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CompilerGeneratedAttribute).Assembly.Location)
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
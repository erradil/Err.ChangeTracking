using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Err.ChangeTracking.SourceGenerator.Tests;

public class NestedTypeGeneratorTests
{
    private const string EmployeeClassText = """
                                             namespace Err.ChangeTracking.SampleDemo;

                                             [Trackable]
                                             internal partial record Employee
                                             {
                                                 public partial string Name { get; set; }
                                                 protected static partial int? Age { get; set; }
                                                 [Trackable] public partial struct Address
                                                 {
                                                     public partial string City { get; set; }
                                                     public partial string Zipcode { get; set; }
                                                 }
                                             }
                                             """;

    private const string ExpectedEmployeeGeneratedText = """
                                                         #nullable enable
                                                         namespace Err.ChangeTracking.SampleDemo;

                                                         // Auto-generated for Employee due to [TrackableAttribute]
                                                         internal partial record Employee : Err.ChangeTracking.ITrackable<Employee>
                                                         {
                                                             private Err.ChangeTracking.IChangeTracking<Employee>? _changeTracker;
                                                             public Err.ChangeTracking.IChangeTracking<Employee> GetChangeTracker() => _changeTracker ??= new Err.ChangeTracking.ChangeTracking<Employee>(this);

                                                             private string _name;
                                                             public partial string Name
                                                             {
                                                                 get => _name;
                                                                 set { _changeTracker?.RecordChange("Name", _name, value); _name = value; }
                                                             }

                                                             private static int? _age;
                                                             protected static partial int? Age
                                                             {
                                                                 get => _age;
                                                                 set { _age = value; }
                                                             }

                                                         }
                                                         """;

    private const string ExpectedAddressGeneratedText = """
                                                        #nullable enable
                                                        namespace Err.ChangeTracking.SampleDemo;

                                                        internal partial record Employee
                                                        {
                                                            // Auto-generated for Address due to [TrackableAttribute]
                                                            public partial struct Address : Err.ChangeTracking.ITrackable<Employee.Address>
                                                            {
                                                                private Err.ChangeTracking.IChangeTracking<Employee.Address>? _changeTracker;
                                                                public Err.ChangeTracking.IChangeTracking<Employee.Address> GetChangeTracker() => _changeTracker ??= new Err.ChangeTracking.ChangeTracking<Employee.Address>(this);

                                                                private string _city;
                                                                public partial string City
                                                                {
                                                                    get => _city;
                                                                    set { _changeTracker?.RecordChange("City", _city, value); _city = value; }
                                                                }

                                                                private string _zipcode;
                                                                public partial string Zipcode
                                                                {
                                                                    get => _zipcode;
                                                                    set { _changeTracker?.RecordChange("Zipcode", _zipcode, value); _zipcode = value; }
                                                                }

                                                            }
                                                        }
                                                        """;

    // Minimal Trackable attribute implementation for testing
    private const string TrackableAttributeText = """
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
    public void GenerateEmployeeRecord()
    {
        // Create an instance of the source generator
        var generator = new PartialPropertyGenerator();

        // Use GeneratorDriver for testing
        var driver = CSharpGeneratorDriver.Create(generator);

        // Create a compilation with the required source code and references
        var compilation = CreateCompilation();

        // Run the generator
        var runResult = driver.RunGenerators(compilation).GetRunResult();

        // Verify Employee.g.cs was generated
        var employeeGeneratedTree = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("Err.ChangeTracking.SampleDemo.Employee.g.cs"));

        Assert.NotNull(employeeGeneratedTree);
        //Assert.Equal(ExpectedEmployeeGeneratedText, employeeGeneratedTree.GetText().ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences: true);

        // Verify nested Address struct was properly generated
        var addressGeneratedTree = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith("Err.ChangeTracking.SampleDemo.Employee.Address.g.cs"));

        Assert.NotNull(addressGeneratedTree);
        //Assert.Equal(ExpectedAddressGeneratedText, addressGeneratedTree.GetText().ToString(), ignoreLineEndingDifferences: true, ignoreWhiteSpaceDifferences:true);
    }

    private static Compilation CreateCompilation()
    {
        return CSharpCompilation.Create("NestedTypeGeneratorTests",
            [
                CSharpSyntaxTree.ParseText(EmployeeClassText),
                CSharpSyntaxTree.ParseText(TrackableAttributeText)
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
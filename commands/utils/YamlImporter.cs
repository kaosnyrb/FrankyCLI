using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace FrankyCLI
{
    public class YamlImporter
    {

        public static T getObjectFrom<T>(string filePath)
        {
            string content = File.ReadAllText(filePath);
            return getObjectFromYaml<T>(content);
        }

        public static T getObjectFromYaml<T>(string yaml)
        {
            var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)  // see height_in_inches in sample yml 
            .Build();

            //yml contains a string containing your YAML
            T obj = deserializer.Deserialize<T>(yaml);
            return obj;

        }
    }
    public class YamlExporter
    {

        public static void WriteObjToYaml(string loc, object obj)
        {
            WriteStringTo(loc, BuildYaml(obj));
        }

        public static string BuildYaml(object obj)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
            var yaml = serializer.Serialize(obj);
            System.Console.WriteLine(yaml);
            return yaml;
        }

        public static void WriteStringTo(string loc, string content)
        {
            try
            {
                // Create or overwrite the file with the specified content.
                File.WriteAllText(loc, content);

                Console.WriteLine("String successfully written to " + loc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to " + ex.Message);
            }
        }
    }
}

using System.IO;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
namespace TinyScholar.Switcher;
[DataContract]
public record Preferences([property:DataMember] bool Light, [property:DataMember] string Storage, [property:DataMember] string Mods);
public static class SettingsStore
{
    public static Preferences Read(string json)
    {
        using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))
        {
            var value=new DataContractJsonSerializer(typeof(Preferences)).ReadObject(stream) as Preferences
                ?? throw new SerializationException("Settings file does not contain preferences.");
            return value with { Storage=value.Storage??"", Mods=value.Mods??"" };
        }
    }
    public static string Write(Preferences value)
    {
        using(var stream=new MemoryStream())
        {
            new DataContractJsonSerializer(typeof(Preferences)).WriteObject(stream,value);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}

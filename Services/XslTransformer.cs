using System.Text;
using System.Xml;
using System.Xml.Xsl;

namespace StudentPerformanceXml.Services;

public class XslTransformer
{
    public string Transform(string xmlContent, string xslContent)
    {
        var xslt = new XslCompiledTransform();
        using (XmlReader xslReader = XmlReader.Create(new StringReader(xslContent)))
        {
            xslt.Load(xslReader);
        }

        using XmlReader xmlReader = XmlReader.Create(new StringReader(xmlContent));
        using var writer = new Utf8StringWriter();
        xslt.Transform(xmlReader, null, writer);
        return writer.ToString();
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}

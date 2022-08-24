using System;
using System.Xml;
using System.Xml.Linq;

namespace Serilog.Extensions
{
    internal static class XmlExtensions
    {
        public static bool TryGetXmlDocument(this string text, out XElement xmlElement)
        {
            xmlElement = null;
            text = text.Trim();
            if ((text.StartsWith("<") && text.EndsWith("/>")))
            {
                try
                {
                    var x = XElement.Load(text);
                    return true;
                }
                catch(Exception) {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
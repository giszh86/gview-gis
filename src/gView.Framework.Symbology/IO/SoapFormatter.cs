﻿using gView.Framework.Core.IO;
using gView.Framework.Common;
using gView.GraphicsEngine;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace gView.Framework.Symbology.IO
{
    class SoapFormatter
    {
        public void Serialize(MemoryStream ms, object obj)
        {
            if (obj == null)
            {
                return;
            }

            // DoTo:
            if (typeof(GraphicsEngine.Abstraction.IFont).IsAssignableFrom(obj.GetType()))
            {
                var xml =
    @"<?xml version=""1.0""?>
<SOAP-ENV:Envelope SOAP-ENV:encodingStyle=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:clr=""http://schemas.microsoft.com/soap/encoding/clr/1.0"" xmlns:SOAP-ENV=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:SOAP-ENC=""http://schemas.xmlsoap.org/soap/encoding/"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
<SOAP-ENV:Body>
<a1:Font xmlns:a1=""http://schemas.microsoft.com/clr/nsassem/System.Drawing/System.Drawing%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Db03f5f7f11d50a3a"" id=""ref-1"">
<Name id=""ref-3"">{font-name}</Name>
<Size>{font-size}</Size>
<Style xmlns:a1=""http://schemas.microsoft.com/clr/nsassem/System.Drawing/System.Drawing%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Db03f5f7f11d50a3a"" xsi:type=""a1:FontStyle"">{font-style}</Style>
<Unit xmlns:a1=""http://schemas.microsoft.com/clr/nsassem/System.Drawing/System.Drawing%2C%20Version%3D4.0.0.0%2C%20Culture%3Dneutral%2C%20PublicKeyToken%3Db03f5f7f11d50a3a"" xsi:type=""a1:GraphicsUnit"">{font-unit}</Unit>
</a1:Font>
</SOAP-ENV:Body>
</SOAP-ENV:Envelope>".Replace("\n", "").Replace("\n", "");

                var font = (GraphicsEngine.Abstraction.IFont)obj;
                xml = xml.Replace("{font-name}", EscapeXml(font.Name));   // Replace & => &amp; , > to &lt;
                xml = xml.Replace("{font-size}", font.Size.ToString().Replace(",", "."));
                xml = xml.Replace("{font-style}", font.Style.ToString());
                xml = xml.Replace("{font-unit}", font.Unit.ToString());

                var bytes = Encoding.ASCII.GetBytes(xml);
                ms.Write(bytes, 0, bytes.Length);
            }
        }

        public object Deserialize<T>(MemoryStream ms, IErrorReport errorReport, object source, bool writeError = false)
        {
            // ToDo:
            if (typeof(T).Equals(typeof(GraphicsEngine.Abstraction.IFont)))
            {
                var soap = Encoding.ASCII.GetString(ms.ToArray());
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(soap);

                string name = "Arial";
                float size = 8;
                FontStyle fontStyle = FontStyle.Regular;
                GraphicsUnit grUnit = GraphicsUnit.Point;

                XmlNode nameNode = doc.SelectSingleNode("//Name");
                if (nameNode != null)
                {
                    name = nameNode.InnerText;
                }

                XmlNode sizeNode = doc.SelectSingleNode("//Size");
                if (sizeNode != null)
                {
                    size = sizeNode.InnerText.ToFloat();
                }

                XmlNode styleNode = doc.SelectSingleNode("//Style");
                if (styleNode != null)
                {
                    fontStyle = (FontStyle)Enum.Parse(typeof(FontStyle), styleNode.InnerText);
                }

                XmlNode unitNode = doc.SelectSingleNode("//Unit");
                if (styleNode != null)
                {
                    grUnit = (GraphicsUnit)Enum.Parse(typeof(GraphicsUnit), unitNode.InnerText);
                }

                var fontFamilyName = Current.Engine.GetInstalledFontNames()
                                            .Where(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
                                            .FirstOrDefault();
                if (string.IsNullOrEmpty(fontFamilyName))
                {
                    fontFamilyName = Current.Engine.GetDefaultFontName();

                    if (errorReport != null)
                    {
                        var message = $"Font '{name}' not installed on target system. '{fontFamilyName}' will be used instead.";
                        if (writeError == true)
                        {
                            errorReport.AddError(message, source);
                        }
                        else
                        {
                            errorReport.AddWarning(message, source);
                        }
                    }
                }

                var font = Current.Engine.CreateFont(fontFamilyName, size, fontStyle, grUnit);
                return font;
            }

            return default(T);
        }

        private string EscapeXml(string toXmlString)
        {
            if (!string.IsNullOrEmpty(toXmlString))
            {
                // replace literal values with entities
                toXmlString = toXmlString.Replace("&", "&amp;");
                toXmlString = toXmlString.Replace("'", "&apos;");
                toXmlString = toXmlString.Replace("\"", "&quot;");
                toXmlString = toXmlString.Replace(">", "&gt;");
                toXmlString = toXmlString.Replace("<", "&lt;");
            }

            return toXmlString;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace MultilingualNovelEditor
{
    internal class EpubProvider
    {
        const int MustBeLessThan = 100000000;
        private static int GetStableHash(string s)
        {
            uint hash = 0;
            // if you care this can be done much faster with unsafe 
            // using fixed char* reinterpreted as a byte*
            foreach (byte b in System.Text.Encoding.Unicode.GetBytes(s))
            {
                hash += b;
                hash += (hash << 10);
                hash ^= (hash >> 6);
            }
            // final avalanche
            hash += (hash << 3);
            hash ^= (hash >> 11);
            hash += (hash << 15);
            // helpfully we only want positive integer < MUST_BE_LESS_THAN
            // so simple truncate cast is ok if not perfect
            return (int)(hash % MustBeLessThan);
        }

        private static int CalculateChecksumDigit(ulong n)
        {
            string sTemp = n.ToString();
            int iSum = 0;

            // Calculate the checksum digit here.
            for (int i = sTemp.Length; i >= 1; i--)
            {
                int iDigit = Convert.ToInt32(sTemp.Substring(i - 1, 1));
                // This appears to be backwards but the 
                // EAN-13 checksum must be calculated
                // this way to be compatible with UPC-A.
                if (i % 2 == 0)
                { // odd  
                    iSum += iDigit * 3;
                }
                else
                { // even
                    iSum += iDigit * 1;
                }
            }
            return (10 - (iSum % 10)) % 10;
        }


        private static string GenerateISBN(string title)
        {
            string titlehash = GetStableHash(title).ToString("D6");
            string fakeisbn = "978001" + titlehash;

            string check = CalculateChecksumDigit(Convert.ToUInt64(fakeisbn)).ToString();

            var a = fakeisbn + check;
            if (a.Length > 13)
            {
                a = a.Substring(0, 13);
            }
            return a;
        }

        class Tz
        {
            public Tz(int id, string xhtml, string title, string idRef)
            {
                Id = id;
                Xhtml = xhtml;
                Title = title;
                IdRef = idRef;
            }

            public int Id { get; }
            public string Xhtml { get; }
            public string Title { get; }
            public string IdRef { get; }
        }

        public void Export(string epubFileName, Novel novel)
        {
            const string CssTemplateName = "cssTemplate_v1.0.css";
            var xxx = Path.GetFileNameWithoutExtension(epubFileName);
            var isbn = GenerateISBN(xxx);
            using (var zipFile = new Ionic.Zip.ZipFile())
            {
                zipFile.CompressionLevel = Ionic.Zlib.CompressionLevel.None;

                const string tocName = "OEBPS\\toc.ncx";
                const string contentName = "OEBPS\\content.opf";

                string tocHtmlName = "OEBPS\\" + isbn + "toc.xhtml";

                var contentIds = new List<Tz>();
                int counter = 0;
                foreach (var chapter in novel)
                {
                    counter++;
                    var id = "c" + counter.ToString().PadLeft(6, '0');
                    var xhtml = "OEBPS\\" + isbn + id + ".xhtml";

                    if (!string.IsNullOrWhiteSpace(chapter.Original.Content))
                    {
                        var contents = chapter.Original.Content.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
                        var content2s= chapter.Translation.Content.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None);
                        if (contents.Length > 0)
                        {
                            using (var chapterStream = new MemoryStream())
                            {
                                chapterStream.WriteUnicode("<html xmlns=\"http://www.w3.org/1999/xhtml\">\r\n");
                                chapterStream.WriteUnicode("<head>\r\n");
                                chapterStream.WriteUnicode("<title>" + chapter.Original.Title + "</title>\r\n");
                                chapterStream.WriteUnicode("<link href=\"" + CssTemplateName +
                                                           "\" rel=\"stylesheet\" type=\"text/css\"/>\r\n");
                                chapterStream.WriteUnicode(
                                    "<meta content=\"urn:uuid:072446c8-f17a-4aeb-b293-b77e018d278d\" name=\"Adept.expected.resource\"/>\r\n");
                                chapterStream.WriteUnicode("</head>\r\n");
                                chapterStream.WriteUnicode("<body>\r\n");
                                chapterStream.WriteUnicode("<p class=\"chaptertitle\">" + chapter.Original.Title + "</p>\r\n");
                                chapterStream.WriteUnicode("<p class=\"chaptertitle\">" + chapter.Translation.Title + "</p>\r\n");
                                chapterStream.WriteUnicode("<hr/>\r\n");
                                    
                                for (int i = 0; i < contents.Length; i++)
                                {
                                    var para = contents[i];
                                    if (string.IsNullOrWhiteSpace(para)) continue;
                                    chapterStream.WriteUnicode("<p>" + para + "</p>\r\n");
                                    chapterStream.WriteUnicode("<p>" + content2s[i] + "</p>\r\n");
                                }

                                chapterStream.WriteUnicode("</body>\r\n");
                                chapterStream.WriteUnicode("</html>\r\n");
                                zipFile.AddEntry(xhtml, chapterStream.ToArray());
                            }

                            //Add Content
                            contentIds.Add(new Tz(counter, isbn + id + ".xhtml", chapter.Original.Title, id));
                        }
                    }
                }

                using (var tocHtmlStream = new MemoryStream())
                {
                    tocHtmlStream.WriteUnicode("<html xmlns=\"http://www.w3.org/1999/xhtml\">\r\n");
                    tocHtmlStream.WriteUnicode("<head>\r\n");
                    tocHtmlStream.WriteUnicode("<title>Table of Contents</title>\r\n");
                    tocHtmlStream.WriteUnicode("<link href=\"" + CssTemplateName +
                                               "\" rel=\"stylesheet\" type=\"text/css\"/>\r\n");
                    tocHtmlStream.WriteUnicode(
                        "<meta content=\"urn:uuid:072446c8-f17a-4aeb-b293-b77e018d278d\" name=\"Adept.expected.resource\"/>\r\n");
                    tocHtmlStream.WriteUnicode("</head>\r\n");
                    tocHtmlStream.WriteUnicode("<body>\r\n");
                    tocHtmlStream.WriteUnicode("<div class=\"story\">\r\n");
                    tocHtmlStream.WriteUnicode("<p class=\"toctitle\">Table of Contents</p>\r\n");
                    tocHtmlStream.WriteUnicode("</div>\r\n");

                    foreach (var item in contentIds)
                    {
                        tocHtmlStream.WriteUnicode("<p class=\"contentschaptertitle\"><a href=\"" + item.Xhtml + "\">" +
                                                   item.Title + "</a></p>\r\n");
                    }

                    tocHtmlStream.WriteUnicode("</div>\r\n");
                    tocHtmlStream.WriteUnicode("</body>\r\n");
                    tocHtmlStream.WriteUnicode("</html>\r\n");
                    zipFile.AddEntry(tocHtmlName, tocHtmlStream.ToArray());
                }

                using (var tocStream = new MemoryStream())
                {
                    tocStream.WriteUnicode("<?xml version=\"1.0\"?>\r\n");
                    tocStream.WriteUnicode(
                        "<!DOCTYPE ncx PUBLIC \" -//NISO//DTD ncx 2005-1//EN\" \"http://www.daisy.org/z3986/2005/ncx-2005-1.dtd\">\r\n");
                    tocStream.WriteUnicode(
                        "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\r\n");
                    tocStream.WriteUnicode("<head>\r\n");
                    tocStream.WriteUnicode("<meta name=\"" + xxx + "\" content=\"\"/>\r\n");
                    tocStream.WriteUnicode("</head>\r\n");
                    tocStream.WriteUnicode("<docTitle>\r\n");
                    tocStream.WriteUnicode("<text>" + xxx + "</text>\r\n");
                    tocStream.WriteUnicode("</docTitle>\r\n");
                    tocStream.WriteUnicode("<navMap>\r\n");

                    foreach (var item in contentIds)
                    {
                        tocStream.WriteUnicode("<navPoint id=\"navpoint-" + item.Id + "\" playOrder=\"" + item.Id +
                                               "\">\r\n");
                        tocStream.WriteUnicode("<navLabel>\r\n");
                        tocStream.WriteUnicode("<text>" + item.Title + "</text>\r\n");
                        tocStream.WriteUnicode("</navLabel>\r\n");
                        tocStream.WriteUnicode("<content src=\"" + item.Xhtml + "\"/>\r\n");
                        tocStream.WriteUnicode("</navPoint>\r\n");
                    }

                    tocStream.WriteUnicode("</navMap>\r\n");
                    tocStream.WriteUnicode("</ncx>\r\n");
                    zipFile.AddEntry(tocName, tocStream.ToArray());
                }

                using (var contentStream = new MemoryStream())
                {
                    contentStream.WriteUnicode(
                        "<package xmlns=\"http://www.idpf.org/2007/opf\" unique-identifier=\"BookId\" version=\"2.0\">\r\n");
                    contentStream.WriteUnicode("<metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\r\n");
                    contentStream.WriteUnicode("<dc:title>" + xxx + "</dc:title>\r\n");
                    contentStream.WriteUnicode("<dc:creator>zunwno</dc:creator>\r\n");
                    contentStream.WriteUnicode("<dc:publisher>ZT</dc:publisher>\r\n");
                    contentStream.WriteUnicode("<dc:format/>\r\n");
                    contentStream.WriteUnicode("<dc:date>" + DateTime.Now.ToString("yyyy-MM-dd") + "</dc:date>\r\n");
                    contentStream.WriteUnicode("<dc:subject/>\r\n");
                    contentStream.WriteUnicode("<dc:description>" + xxx + "</dc:description>\r\n");
                    contentStream.WriteUnicode("<dc:rights/>\r\n");
                    contentStream.WriteUnicode("<dc:language>en</dc:language>\r\n");
                    contentStream.WriteUnicode("<dc:identifier id=\"BookId\">" + isbn + "</dc:identifier>\r\n");
                    contentStream.WriteUnicode("<meta content=\"cover\" name=\"cover\"/>\r\n");
                    contentStream.WriteUnicode("</metadata>\r\n");
                    contentStream.WriteUnicode("<manifest>\r\n");

                    contentStream.WriteUnicode("<item href=\"" + CssTemplateName +
                                               "\" id=\"cssTemplate\" media-type=\"text/css\"/>\r\n");
                    contentStream.WriteUnicode(
                        "<item href=\"toc.ncx\" id=\"ncx\" media-type=\"application/x-dtbncx+xml\"/>\r\n");
                    contentStream.WriteUnicode("<item href=\"" + isbn +
                                               "toc.xhtml\" id=\"toc\" media-type=\"application/xhtml+xml\"/>\r\n");
                    foreach (Tz item in contentIds)
                    {
                        contentStream.WriteUnicode("<item href=\"" + item.Xhtml + "\" id=\"" + item.IdRef +
                                                   "\" media-type=\"application/xhtml+xml\"/>\r\n");
                    }

                    contentStream.WriteUnicode("</manifest>\r\n");
                    contentStream.WriteUnicode("<spine toc=\"ncx\">\r\n");

                    contentStream.WriteUnicode("<itemref idref=\"toc\" linear=\"yes\"/>\r\n");
                    foreach (Tz item in contentIds)
                    {
                        contentStream.WriteUnicode("<itemref idref=\"" + item.IdRef + "\" linear=\"yes\"/>\r\n");
                    }

                    contentStream.WriteUnicode("</spine>\r\n");
                    contentStream.WriteUnicode("<guide>\r\n");
                    contentStream.WriteUnicode("<reference href=\"" + isbn +
                                               "toc.xhtml\" title=\"Table of Contents\" type=\"toc\"/>\r\n");
                    contentStream.WriteUnicode("</guide>\r\n");
                    contentStream.WriteUnicode("</package>\r\n");
                    zipFile.AddEntry(contentName, contentStream.ToArray());
                }

                using (var cssStream = new MemoryStream())
                {
                    cssStream.WriteUnicode(Properties.Resources.cssTemplate_v1_0);
                    zipFile.AddEntry("OEBPS\\" + CssTemplateName, cssStream.ToArray());
                }

                using (var containerStream = new MemoryStream())
                {
                    containerStream.WriteUnicode("<?xml version=\"1.0\"?>\r");
                    containerStream.WriteUnicode(
                        "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\r");
                    containerStream.WriteUnicode("  <rootfiles>\r");
                    containerStream.WriteUnicode(
                        "    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/>\r");
                    containerStream.WriteUnicode("  </rootfiles>\r");
                    containerStream.WriteUnicode("</container>\r");
                    zipFile.AddEntry("META-INF\\container.xml", containerStream.ToArray());
                }

                using (var mimetypeStream = new MemoryStream())
                {
                    mimetypeStream.WriteUnicode("application/epub+zip");
                    zipFile.AddEntry("mimetype", mimetypeStream.ToArray());
                }

                zipFile.Save(epubFileName);
            }
        }
    }
}
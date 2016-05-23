/* Copyright 2010-2016 MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using NUnit.Framework;

namespace MongoDB.Bson.Tests.IO
{
    [TestFixture]
    public class BsonBinaryReaderTests
    {
        [Test]
        [Explicit] // because it creates a huge temp file and takes a long time to execute
        public void BsonBinaryReader_should_support_reading_more_than_2GB()
        {
            var binaryData = new BsonBinaryData(new byte[1024 * 1024]);

            var tempFileName = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFileName, FileMode.Open))
                {
                    using (var binaryWriter = new BsonBinaryWriter(stream))
                    {
                        while (stream.Position < (long)int.MaxValue * 4)
                        {
                            binaryWriter.WriteStartDocument();
                            binaryWriter.WriteName("x");
                            binaryWriter.WriteBinaryData(binaryData);
                            binaryWriter.WriteEndDocument();
                        }
                    }

                    var endOfFilePosition = stream.Position;
                    stream.Position = 0;

                    using (var binaryReader = new BsonBinaryReader(stream))
                    {
                        while (!binaryReader.IsAtEndOfFile())
                        {
                            binaryReader.ReadStartDocument();
                            var bookmark = binaryReader.GetBookmark();

                            binaryReader.ReadName("x");
                            binaryReader.ReturnToBookmark(bookmark);

                            binaryReader.ReadName("x");
                            var readBinaryData = binaryReader.ReadBinaryData();
                            Assert.That(readBinaryData.Bytes.Length, Is.EqualTo(binaryData.Bytes.Length));

                            binaryReader.ReadEndDocument();
                        }
                    }

                    Assert.That(stream.Position, Is.EqualTo(endOfFilePosition));
                }
            }
            finally
            {
                try
                {
                    File.Delete(tempFileName);
                }
                catch
                {
                    // ignore exceptions
                }
            }
        }

        [Test]
        public void BsonBinaryReader_should_support_reading_multiple_documents(
            [Range(0, 3)]
            int numberOfDocuments)
        {
            var document = new BsonDocument("x", 1);
            var bson = document.ToBson();
            var input = Enumerable.Repeat(bson, numberOfDocuments).Aggregate(Enumerable.Empty<byte>(), (a, b) => a.Concat(b)).ToArray();
            var expectedResult = Enumerable.Repeat(document, numberOfDocuments);

            using (var stream = new MemoryStream(input))
            using (var binaryReader = new BsonBinaryReader(stream))
            {
                var result = new List<BsonDocument>();

                while (!binaryReader.IsAtEndOfFile())
                {
                    binaryReader.ReadStartDocument();
                    var name = binaryReader.ReadName();
                    var value = binaryReader.ReadInt32();
                    binaryReader.ReadEndDocument();

                    var resultDocument = new BsonDocument(name, value);
                    result.Add(resultDocument);
                }

                result.Should().Equal(expectedResult);
            }
        }

        [TestCase("00000000 f0 6100", "a")]
        [TestCase("00000000 08 6100 00 f0 6200", "b")]
        [TestCase("00000000 03 6100 00000000 f0 6200", "a.b")]
        [TestCase("00000000 03 6100 00000000 08 6200 00 f0 6300", "a.c")]
        [TestCase("00000000 04 6100 00000000 f0", "a.0")]
        [TestCase("00000000 04 6100 00000000 08 3000 00 f0", "a.1")]
        [TestCase("00000000 04 6100 00000000 03 3000 00000000 f0 6200", "a.0.b")]
        [TestCase("00000000 04 6100 00000000 03 3000 00000000 08 6200 00 f0 6300", "a.0.c")]
        [TestCase("00000000 04 6100 00000000 08 3000 00 03 3100 00000000 f0 6200", "a.1.b")]
        [TestCase("00000000 04 6100 00000000 08 3000 00 03 3200 00000000 08 6200 00 f0 6300", "a.1.c")]
        public void ReadBsonType_should_throw_when_bson_type_is_invalid(string hexBytes, string expectedElementName)
        {
            var bytes = BsonUtils.ParseHexString(hexBytes.Replace(" ", ""));
            var expectedMessage = $"Detected unknown BSON type \"\\xf0\" for fieldname \"{expectedElementName}\". Are you using the latest driver version?";

            using (var memoryStream = new MemoryStream(bytes))
            using (var subject = new BsonBinaryReader(memoryStream))
            {
                Action action = () => BsonSerializer.Deserialize<BsonDocument>(subject);

                action.ShouldThrow<FormatException>().WithMessage(expectedMessage);
            }
        }

        [Test]
        public void TestHelloWorld()
        {
            string byteString = @"\x16\x00\x00\x00\x02hello\x00\x06\x00\x00\x00world\x00\x00";
            byte[] bytes = DecodeByteString(byteString);
            var stream = new MemoryStream(bytes);
            using (var bsonReader = new BsonBinaryReader(stream))
            {
                bsonReader.ReadStartDocument();
                Assert.AreEqual(BsonType.String, bsonReader.ReadBsonType());
                Assert.AreEqual("hello", bsonReader.ReadName());
                Assert.AreEqual("world", bsonReader.ReadString());
                bsonReader.ReadEndDocument();
            }
        }

        [Test]
        public void TestBsonAwesome()
        {
            string byteString = @"1\x00\x00\x00\x04BSON\x00&\x00\x00\x00\x020\x00\x08\x00\x00\x00awesome\x00\x011\x00333333\x14@\x102\x00\xc2\x07\x00\x00\x00\x00";
            byte[] bytes = DecodeByteString(byteString);
            var stream = new MemoryStream(bytes);
            using (var bsonReader = new BsonBinaryReader(stream))
            {
                bsonReader.ReadStartDocument();
                Assert.AreEqual(BsonType.Array, bsonReader.ReadBsonType());
                Assert.AreEqual("BSON", bsonReader.ReadName());
                bsonReader.ReadStartArray();
                Assert.AreEqual(BsonType.String, bsonReader.ReadBsonType());
                Assert.AreEqual("awesome", bsonReader.ReadString());
                Assert.AreEqual(BsonType.Double, bsonReader.ReadBsonType());
                Assert.AreEqual(5.05, bsonReader.ReadDouble());
                Assert.AreEqual(BsonType.Int32, bsonReader.ReadBsonType());
                Assert.AreEqual(1986, bsonReader.ReadInt32());
                bsonReader.ReadEndArray();
                bsonReader.ReadEndDocument();
            }
        }

        [Test]
        public void TestIsAtEndOfFileWithTwoDocuments()
        {
            var expected = new BsonDocument("x", 1);

            byte[] bson;
            using (var stream = new MemoryStream())
            using (var writer = new BsonBinaryWriter(stream))
            {
                BsonSerializer.Serialize(writer, expected);
                BsonSerializer.Serialize(writer, expected);
                bson = stream.ToArray();
            }

            using (var stream = new MemoryStream(bson))
            using (var reader = new BsonBinaryReader(stream))
            {
                var count = 0;
                while (!reader.IsAtEndOfFile())
                {
                    var document = BsonSerializer.Deserialize<BsonDocument>(reader);
                    Assert.AreEqual(expected, document);
                    count++;
                }
                Assert.AreEqual(2, count);
            }
        }

        [Test]
        public void TestReadRawBsonArray()
        {
            var bsonDocument = new BsonDocument { { "_id", 1 }, { "A", new BsonArray { 1, 2 } } };
            var bson = bsonDocument.ToBson();

            using (var document = BsonSerializer.Deserialize<CWithRawBsonArray>(bson))
            {
                Assert.AreEqual(1, document.Id);
                Assert.AreEqual(2, document.A.Count);
                Assert.AreEqual(1, document.A[0].AsInt32);
                Assert.AreEqual(2, document.A[1].AsInt32);
                Assert.IsTrue(bson.SequenceEqual(document.ToBson()));
            }
        }

        [Test]
        public void TestReadRawBsonDocument()
        {
            var document = new BsonDocument { { "x", 1 }, { "y", 2 } };
            var bson = document.ToBson();

            using (var rawDocument = BsonSerializer.Deserialize<RawBsonDocument>(bson))
            {
                Assert.AreEqual(1, rawDocument["x"].ToInt32());
                Assert.AreEqual(2, rawDocument["y"].ToInt32());
                Assert.IsTrue(bson.SequenceEqual(rawDocument.ToBson()));
            }
        }

        // private methods
        private static string __hexDigits = "0123456789abcdef";

        private byte[] DecodeByteString(string byteString)
        {
            List<byte> bytes = new List<byte>(byteString.Length);
            for (int i = 0; i < byteString.Length; )
            {
                char c = byteString[i++];
                if (c == '\\' && ((c = byteString[i++]) != '\\'))
                {
                    int x = __hexDigits.IndexOf(char.ToLower(byteString[i++]));
                    int y = __hexDigits.IndexOf(char.ToLower(byteString[i++]));
                    bytes.Add((byte)(16 * x + y));
                }
                else
                {
                    bytes.Add((byte)c);
                }
            }
            return bytes.ToArray();
        }

        // nested classes
        private class CWithRawBsonArray : IDisposable
        {
            public int Id { get; set; }
            public RawBsonArray A { get; set; }

            public void Dispose()
            {
                if (A != null)
                {
                    A.Dispose();
                    A = null;
                }
            }
        }
    }
}

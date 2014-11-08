using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;
using Grax.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InterpolationFormatProviderTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Test45()
        {
            var data = new Dictionary<string, DateTime> 
            { 
                { "Anniversary", new DateTime(2003, 5, 23) },
                { "FirstBirthday", new DateTime(1979, 6, 6) }
            };

            var person1 = new { FirstName = "Bob", LastName = "Grax", BirthDate = new DateTime(1978, 6, 6), SignificantDates = data };
            var person2 = new { FirstName = (string)null, LastName = "Jackson", BirthDate = new DateTime(1978, 6, 6), SignificantDates = data };
            var person3 = new { FirstName = "Jill", LastName = "Grax", BirthDate = new DateTime(1975, 2, 6), SignificantDates = data };

            var result1 = string.Format(new InterpolationFormatProvider(), "My name is {0:FirstName} {0:LastName} and I was born on {0:BirthDate:D}", person1);
            Assert.AreEqual("My name is Bob Grax and I was born on Tuesday, June 6, 1978", result1);

            var result2 = string.Format(new InterpolationFormatProvider(), "My name is {0:FirstName} {0:LastName} and I was married on {0:SignificantDates:Anniversary:D}.  FirstBirthday {1:FirstBirthday:D}", person1, data);
            Assert.AreEqual("My name is Bob Grax and I was married on Friday, May 23, 2003.  FirstBirthday Wednesday, June 6, 1979", result2);

            var result3 = string.Format(new InterpolationFormatProvider(), "My name is {0:FirstName} {0:LastNameX} and I was married on {0:SignificantDates:AnniversaryY:D}.  FirstBirthday {1:FirstBirthday:D}", person2, data);
            Assert.AreEqual("My name is   and I was married on .  FirstBirthday Wednesday, June 6, 1979", result3);

            var result4 = string.Format(new InterpolationFormatProvider(), "SomeNum: {0:SomeNum} OtherNum:{0:OtherNum}", new TestThingy { SomeNum = 99, OtherNum = 77 });
            Assert.AreEqual("SomeNum: 99 OtherNum:77", result4);

            var result5 = (new FormattableThing { }).ToString("Description");
            Assert.AreEqual("My Bologna", result5);

            var ifp = new InterpolationFormatProvider();

            var format = ifp.GetFormat(typeof(string));

            var doFormat = ifp.Format("", null, ifp);
            Assert.AreEqual("", doFormat);

            var result6 = string.Format(new InterpolationFormatProvider(), "My name is {0:FirstName} and I have {1:C} aka {1}", person1, 8834.23m);
            Assert.AreEqual("My name is Bob and I have $8,834.23 aka 8834.23", result6);

            var result7 = (new FormattableThing()).ToString("InterestingDate:D");
            Assert.AreEqual("Friday, December 31, 1999", result7);

            var result8 = (new FormattableThing()).ToString("");
            Assert.AreEqual("", result8);

            var result9 = (new FormattableThing()).ToString(null);
            Assert.IsTrue(result9.EndsWith(typeof(FormattableThing).Name));

            var result10 = string.Format(new InterpolationFormatProvider(), "{0}", DateTime.Now);
            Assert.IsTrue(result10.Length > 0);



        }

        [TestMethod]
        public void Test45Expando()
        {
            dynamic d = new ExpandoObject();
            d.Location = "World";

            var str = "Hello, {0:Location}{0:Unknown}".ToInterpolatedString((object)d);  // returns "Hello, World"

            Assert.AreEqual("Hello, World", str);

            dynamic x = new ExpandoObject();
            x.Location = "World";

            str = "Hello, {0:Location}{0:Unknown}".ToInterpolatedString((object)x);  // returns "Hello, World"

            Assert.AreEqual("Hello, World", str);
        }

        public class FormattableThing : IFormattable
        {
            public string Description { get { return "My Bologna"; } }

            public DateTime InterestingDate { get { return new DateTime(1999, 12, 31); } }

            public string ToString(string format)
            {
                return ToString(format, null);
            }

            public string ToString(string format, IFormatProvider formatProvider)
            {
                formatProvider = formatProvider ?? new InterpolationFormatProvider(this);
                var customFormatProvider = formatProvider as ICustomFormatter;
                if (customFormatProvider != null)
                {
                    return customFormatProvider.Format(format, this, formatProvider);
                }
                else
                {
                    return string.Format(formatProvider, "{0:" + format + "}", this);
                }
            }
        }

       
    }
}

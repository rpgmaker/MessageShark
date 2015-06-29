using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace MessageShark.Tests {
    /// <summary>
    /// Summary description for StreamSerializationTest
    /// </summary>
    [TestClass]
    public class StreamSerializationTest {
        public StreamSerializationTest() {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void ShouldSerializeMessageToMemoryStream() {
            using (var ms = new MemoryStream()) {
                var message = new Message() { ID = 10, CreateDate = DateTime.Now, Data = "This is a test", test = new Test() { Int = 100, Str = "Testing", UUID = Guid.NewGuid() } };
                MessageSharkSerializer.Serialize(message, ms);
                var buffer = ms.ToArray();
            }
        }


        [TestMethod]
        public void ShouldSerializeMessageWithObjectToMemoryStream() {
            using (var ms = new MemoryStream()) {
                object message = new Message() { ID = 10, CreateDate = DateTime.Now, Data = "This is a test", test = new Test() { Int = 100, Str = "Testing", UUID = Guid.NewGuid() } };
                MessageSharkSerializer.Serialize(message, ms);
                var buffer = ms.ToArray();
            }
        }
    }
}

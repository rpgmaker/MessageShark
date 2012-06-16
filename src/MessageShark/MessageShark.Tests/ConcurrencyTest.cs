using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;

namespace MessageShark.Tests {
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ConcurrencyTest {
        public ConcurrencyTest() {
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

        public void Handle(Task t) {
            var ex = t.Exception.ToString();
        }

        [TestMethod]
        public void TestMultipleSerialization() {
            //MessageSharkSerializer.Build();
            var count = 10000;
            var tasks = new Task[count];
            //var threads = new ConcurrentBag<int>();
            for (var i = 0; i < count; i++) {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    var data = new Message() { ID = 10, CreateDate = DateTime.Now, Data = "This is a test" };
                    var buffer = MessageSharkSerializer.Serialize(data);
                    //threads.Add(Thread.CurrentThread.ManagedThreadId);
                }).ContinueWith(t => Handle(t),   TaskContinuationOptions.OnlyOnFaulted)
                .ContinueWith(_ => {});
            }
            Task.WaitAll(tasks);
        }
    }
}

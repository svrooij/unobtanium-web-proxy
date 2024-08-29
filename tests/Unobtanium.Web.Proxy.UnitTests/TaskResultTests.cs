using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unobtanium.Web.Proxy.StreamExtended.Network;

namespace Unobtanium.Web.Proxy.UnitTests
{
    [TestClass]
    public class TaskResultTests
    {
        [TestMethod]
        public void TaskResult_ShouldWrapTaskCorrectly ()
        {
            var task = Task.CompletedTask;
            var state = new object();
            var taskResult = new TaskResult(task, state);

            Assert.AreEqual(state, taskResult.AsyncState);
            Assert.IsTrue(taskResult.IsCompleted);
            Assert.IsFalse(taskResult.CompletedSynchronously);
        }

        [TestMethod]
        public void TaskResultT_ShouldWrapTaskCorrectly ()
        {
            var task = Task.FromResult(42);
            var state = new object();
            var taskResult = new TaskResult<int>(task, state);

            Assert.AreEqual(state, taskResult.AsyncState);
            Assert.AreEqual(42, taskResult.Result);
            Assert.IsTrue(taskResult.IsCompleted);
            Assert.IsFalse(taskResult.CompletedSynchronously);
        }
    }
}

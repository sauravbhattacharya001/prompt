using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Prompt.Tests
{
    [TestClass()]
    public class MainTests
    {
        [TestMethod()]
        public async Task GetResponseTest()
        {
            var response = await Main.GetResponseTest("Hello");
            Assert.IsNotNull(response);
        }
    }
}
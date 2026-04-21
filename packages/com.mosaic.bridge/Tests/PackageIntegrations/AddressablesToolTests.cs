#if MOSAIC_HAS_ADDRESSABLES
using NUnit.Framework;
using Mosaic.Bridge.Tools.Addressables;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class AddressablesToolTests
    {
        [Test]
        public void Groups_List_ReturnsSuccess()
        {
            var result = AddressablesGroupsTool.Groups(new AddressablesGroupsParams
            {
                Action = "list"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.Groups);
        }

        [Test]
        public void Groups_InvalidAction_ReturnsFail()
        {
            var result = AddressablesGroupsTool.Groups(new AddressablesGroupsParams
            {
                Action = "invalid"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Info_ReturnsSettings()
        {
            var result = AddressablesInfoTool.Info(new AddressablesInfoParams());
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
        }

        [Test]
        public void Mark_InvalidAssetPath_ReturnsFail()
        {
            var result = AddressablesMarkTool.Mark(new AddressablesMarkParams
            {
                AssetPath = "Assets/NonExistent/fake.asset"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Groups_CreateAndDelete_RoundTrip()
        {
            var createResult = AddressablesGroupsTool.Groups(new AddressablesGroupsParams
            {
                Action = "create", GroupName = "MosaicTestGroup"
            });
            Assert.IsTrue(createResult.Success, createResult.Error);

            var deleteResult = AddressablesGroupsTool.Groups(new AddressablesGroupsParams
            {
                Action = "delete", GroupName = "MosaicTestGroup"
            });
            Assert.IsTrue(deleteResult.Success, deleteResult.Error);
        }
    }
}
#endif

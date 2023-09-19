using Vulkan;

namespace Veldrid.Vk
{
    public unsafe partial struct VkPhysicalDeviceShaderDrawParameterFeatures
    {
        private const int PhysicalDeviceShaderDrawParameterFeatures = 1000063000;

        public VkStructureType sType;
        public void* pNext;
        public VkBool32 shaderDrawParameters;
        public static VkPhysicalDeviceShaderDrawParameterFeatures New()
        {
            VkPhysicalDeviceShaderDrawParameterFeatures ret = new VkPhysicalDeviceShaderDrawParameterFeatures();
            ret.sType = (VkStructureType)PhysicalDeviceShaderDrawParameterFeatures;//.PhysicalDeviceShaderDrawParameterFeatures;
            return ret;
        }
    }
}

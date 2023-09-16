using System;
using System.Collections.Generic;
using Vulkan;
using static Vulkan.VulkanNative;
using static Veldrid.Vk.VulkanUtil;

namespace Veldrid.Vk
{
    internal unsafe class VkResourceSet : ResourceSet
    {
        private readonly VkGraphicsDevice _gd;
        private readonly DescriptorResourceCounts _descriptorCounts;
        private readonly DescriptorAllocationToken _descriptorAllocationToken;
        private readonly List<ResourceRefCount> _refCounts = new List<ResourceRefCount>();
        private bool _destroyed;
        private string _name;

        public VkDescriptorSet DescriptorSet => _descriptorAllocationToken.Set;

        private readonly List<VkTexture> _sampledTextures = new List<VkTexture>();
        public List<VkTexture> SampledTextures => _sampledTextures;
        private readonly List<VkTexture> _storageImages = new List<VkTexture>();
        public List<VkTexture> StorageTextures => _storageImages;

        public ResourceRefCount RefCount { get; }
        public List<ResourceRefCount> RefCounts => _refCounts;

        public override bool IsDisposed => _destroyed;

        public VkResourceSet(VkGraphicsDevice gd, ref ResourceSetDescription description)
            : base(ref description)
        {
            _gd = gd;
            RefCount = new ResourceRefCount(DisposeCore);
            VkResourceLayout vkLayout = Util.AssertSubtype<ResourceLayout, VkResourceLayout>(description.Layout);

            VkDescriptorSetLayout dsl = vkLayout.DescriptorSetLayout;
            _descriptorCounts = vkLayout.DescriptorResourceCounts;
            _descriptorAllocationToken = _gd.DescriptorPoolManager.Allocate(_descriptorCounts, dsl);

            BindableResource[] boundResources = description.BoundResources;
            uint descriptorWriteCount = (uint)description.Layout.Description.Elements.Length;
            VkWriteDescriptorSet* descriptorWrites = stackalloc VkWriteDescriptorSet[(int)descriptorWriteCount];
            VkDescriptorBufferInfo* bufferInfos = stackalloc VkDescriptorBufferInfo[(int)boundResources.Length];
            VkDescriptorImageInfo* imageInfos = stackalloc VkDescriptorImageInfo[(int)boundResources.Length];

            int resourceIndex = 0;
            for (int i = 0; i < descriptorWriteCount; i++)
            {
                VkDescriptorType type = vkLayout.DescriptorTypes[i];

                descriptorWrites[i].sType = VkStructureType.WriteDescriptorSet;
                descriptorWrites[i].descriptorCount = description.Layout.Description.Elements[i].ArrayCount;
                descriptorWrites[i].descriptorType = type;
                descriptorWrites[i].dstBinding = (uint)i;
                descriptorWrites[i].dstSet = _descriptorAllocationToken.Set;

                int baseResourceIndex = resourceIndex;
                if (type == VkDescriptorType.UniformBuffer || type == VkDescriptorType.UniformBufferDynamic
                    || type == VkDescriptorType.StorageBuffer || type == VkDescriptorType.StorageBufferDynamic)
                {
                    for (int arrIndex = 0; arrIndex < descriptorWrites[i].descriptorCount; arrIndex++)
                    {
                        DeviceBufferRange range = Util.GetBufferRange(boundResources[resourceIndex], 0);
                        VkBuffer rangedVkBuffer = Util.AssertSubtype<DeviceBuffer, VkBuffer>(range.Buffer);
                        bufferInfos[resourceIndex].buffer = rangedVkBuffer.DeviceBuffer;
                        bufferInfos[resourceIndex].offset = range.Offset;
                        bufferInfos[resourceIndex].range = range.SizeInBytes;
                        _refCounts.Add(rangedVkBuffer.RefCount);

                        resourceIndex++;
                    }
                    descriptorWrites[i].pBufferInfo = &bufferInfos[baseResourceIndex];
                }
                else if (type == VkDescriptorType.SampledImage)
                {
                    for (int arrIndex = 0; arrIndex < descriptorWrites[i].descriptorCount; arrIndex++)
                    {
                        TextureView texView = Util.GetTextureView(_gd, boundResources[resourceIndex]);
                        VkTextureView vkTexView = Util.AssertSubtype<TextureView, VkTextureView>(texView);
                        imageInfos[resourceIndex].imageView = vkTexView.ImageView;
                        imageInfos[resourceIndex].imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                        _sampledTextures.Add(Util.AssertSubtype<Texture, VkTexture>(texView.Target));
                        _refCounts.Add(vkTexView.RefCount);

                        resourceIndex++;
                    }
                    descriptorWrites[i].pImageInfo = &imageInfos[baseResourceIndex];
                }
                else if (type == VkDescriptorType.StorageImage)
                {
                    for (int arrIndex = 0; arrIndex < descriptorWrites[i].descriptorCount; arrIndex++)
                    {
                        TextureView texView = Util.GetTextureView(_gd, boundResources[resourceIndex]);
                        VkTextureView vkTexView = Util.AssertSubtype<TextureView, VkTextureView>(texView);
                        imageInfos[resourceIndex].imageView = vkTexView.ImageView;
                        imageInfos[resourceIndex].imageLayout = VkImageLayout.General;
                        _storageImages.Add(Util.AssertSubtype<Texture, VkTexture>(texView.Target));
                        _refCounts.Add(vkTexView.RefCount);

                        resourceIndex++;
                    }
                    descriptorWrites[i].pImageInfo = &imageInfos[baseResourceIndex];
                }
                else if (type == VkDescriptorType.Sampler)
                {
                    for (int arrIndex = 0; arrIndex < descriptorWrites[i].descriptorCount; arrIndex++)
                    {
                        VkSampler sampler = Util.AssertSubtype<BindableResource, VkSampler>(boundResources[resourceIndex]);
                        imageInfos[resourceIndex].sampler = sampler.DeviceSampler;
                        _refCounts.Add(sampler.RefCount);

                        resourceIndex++;
                    }
                    descriptorWrites[i].pImageInfo = &imageInfos[baseResourceIndex];
                }
                else
                    throw new ArgumentOutOfRangeException($"Descriptior type {type} not supported.");
            }

            vkUpdateDescriptorSets(_gd.Device, descriptorWriteCount, descriptorWrites, 0, null);
        }

        public override string Name
        {
            get => _name;
            set
            {
                _name = value;
                _gd.SetResourceName(this, value);
            }
        }

        public override void Dispose()
        {
            RefCount.Decrement();
        }

        private void DisposeCore()
        {
            if (!_destroyed)
            {
                _destroyed = true;
                _gd.DescriptorPoolManager.Free(_descriptorAllocationToken, _descriptorCounts);
            }
        }
    }
}

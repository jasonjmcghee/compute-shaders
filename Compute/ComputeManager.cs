using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Godot.Collections;

namespace ComputeShader.Compute; 

public class ComputeManager {
    public readonly RenderingDevice Rd;
    public readonly Godot.Collections.Dictionary<int, Rid> RidLookup = new();

    private readonly List<ComputePipeline> _pipelines = new();

    public ComputeManager() {
        Rd = RenderingServer.CreateLocalRenderingDevice();
    }

    public ComputePipeline AddPipeline(string shaderPath, uint xGroups, uint yGroups, uint zGroups) {
        ComputePipeline pipeline = new ComputePipeline(this, shaderPath, xGroups, yGroups, zGroups);
        _pipelines.Add(pipeline);
        return pipeline;
    }
    
    public void Execute() {
        foreach (ComputePipeline pipeline in _pipelines) {
            pipeline.Execute();
        }
        
        Rd.Submit();
        Rd.Sync();
    }
    
    public void UpdateBuffer<T>(int bufferId, T obj) where T : struct {
        UpdateBuffer(bufferId, ConvertToBytes(obj));
    }
    
    public void UpdateBuffer<T>(int bufferId, T[] objects) where T : struct {
        UpdateBuffer(bufferId, ConvertArrayToBytes(objects));
    }

    public void UpdateBuffer(int bufferId, byte[] data) {
        Rd.BufferUpdate(RidLookup[bufferId], 0, (uint) data.Length, data);
    }

    public void ClearBuffer(int bufferId, int length) {
        Rd.BufferClear(RidLookup[bufferId], 0, (uint) length);
    }
    
    public byte[] GetDataFromBuffer(int bufferId) {
        return Rd.BufferGetData(RidLookup[bufferId], 0);
    }
    
    public byte[] GetDataFromBuffer(int bufferId, uint size) {
        return Rd.BufferGetData(RidLookup[bufferId], 0, size);
    }

    public void CleanUp() {
        foreach (ComputePipeline pipeline in _pipelines) {
            pipeline.CleanUp();
        }

        Rd.Free();
    }

    internal static byte[] ConvertToBytes<T>(T param) where T : struct {
        var size = Marshal.SizeOf(param);
        var arr = new byte[size];

        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(param, ptr, true);
        Marshal.Copy(ptr, arr, 0, size);
        Marshal.FreeHGlobal(ptr);

        return arr;
    }

    internal static byte[] ConvertArrayToBytes<T>(T[] param) where T : struct {
        int size = Marshal.SizeOf<T>();
        byte[] arr = new byte[param.Length * size];

        // Pin the array in memory so GC won't move it, then copy
        GCHandle pin = GCHandle.Alloc(param, GCHandleType.Pinned);
        IntPtr srcPtr = pin.AddrOfPinnedObject();

        Marshal.Copy(srcPtr, arr, 0, arr.Length);

        // Don't forget to unpin when you're done
        pin.Free();

        return arr;
    }
}

public class ComputePipeline {
    private readonly ComputeManager manager;
    private Array<RDUniform> uniforms;
    private Rid pipeline;
    private Rid shader;
    private Rid uniformSet;
    private Array<Rid> to_free = new();
    private readonly RenderingDevice rd;

    private uint xGroups;
    private uint yGroups;
    private uint zGroups;

    private bool isBuilt = false;

    public ComputePipeline(ComputeManager manager, string shaderPath, uint xGroups, uint yGroups, uint zGroups) {
        this.xGroups = xGroups;
        this.yGroups = yGroups;
        this.zGroups = zGroups;
        this.manager = manager;
        rd = manager.Rd;
        AddShader(shaderPath);
        uniforms = new Array<RDUniform>();
    }
    
    public ComputePipeline StoreAndAddStep<T>(int bufferId, T obj) where T : struct {
        manager.RidLookup[bufferId] = CreateStorageBufferOnRd(ComputeManager.ConvertToBytes(obj));
        return this;
    }
    
    public ComputePipeline StoreAndAddStep<T>(int bufferId, T[] objects) where T : struct {
        manager.RidLookup[bufferId] = CreateStorageBufferOnRd(ComputeManager.ConvertArrayToBytes(objects));
        return this;
    }
    
    public ComputePipeline AddStep(int referenceBufferId) {
        CreateStorageBufferOnRdWithRid(manager.RidLookup[referenceBufferId]);
        return this;
    }

    public ComputePipeline Build() {
        uniformSet = rd.UniformSetCreate(uniforms, shader, 0);
        pipeline = rd.ComputePipelineCreate(shader);

        to_free.Add(uniformSet);
        to_free.Add(pipeline);
        isBuilt = true;
        return this;
    }

    public void Execute() {
        if (!isBuilt) {
            throw new Exception("Must call `Build` before executing.");
        }
        var computeList = rd.ComputeListBegin();
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListBindUniformSet(computeList, uniformSet, 0);
        rd.ComputeListDispatch(computeList, xGroups, yGroups, zGroups);
        rd.ComputeListEnd();
    }

    private void AddShader(string shaderPath) {
        var compute = GD.Load<RDShaderFile>(shaderPath);
        shader = rd.ShaderCreateFromSpirV(compute.GetSpirV());
        to_free.Add(shader);
    }
    
    private Rid CreateStorageBufferOnRd(byte[] bytes) {
        var buffer = rd.StorageBufferCreate((uint) bytes.Length, bytes);
        to_free.Add(buffer);

        return CreateStorageBufferOnRdWithRid(buffer);
    }

    private Rid CreateStorageBufferOnRdWithRid(Rid buffer) {
        uniforms.Add(new RDUniform {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = uniforms.Count,
            _Ids = new Array<Rid> {buffer}
        });

        return buffer;
    }
    
    public void CleanUp() {
        foreach (Rid rid in to_free) {
            rd.FreeRid(rid);
        }
    }
}
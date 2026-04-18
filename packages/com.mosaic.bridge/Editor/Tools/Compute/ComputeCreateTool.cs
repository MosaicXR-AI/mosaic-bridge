using System;
using System.IO;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Compute
{
    public static class ComputeCreateTool
    {
        [MosaicTool("compute/create",
                    "Scaffolds a .compute shader file from a template with a companion C# manager script",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ComputeCreateResult> Execute(ComputeCreateParams p)
        {
            if (string.IsNullOrEmpty(p.Template))
                return ToolResult<ComputeCreateResult>.Fail(
                    "Template is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Name))
                return ToolResult<ComputeCreateResult>.Fail(
                    "Name is required", ErrorCodes.INVALID_PARAM);

            string template = p.Template.ToLowerInvariant();
            if (template != "fluid" && template != "boid" && template != "noise" && template != "custom")
                return ToolResult<ComputeCreateResult>.Fail(
                    "Template must be one of: fluid, boid, noise, custom",
                    ErrorCodes.INVALID_PARAM);

            string outDir = string.IsNullOrEmpty(p.OutputDirectory)
                ? "Assets/Generated/ComputeShaders"
                : p.OutputDirectory;

            string absoluteDir = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "..", outDir));
            Directory.CreateDirectory(absoluteDir);

            string computeSource;
            string managerSource;
            string[] kernelNames;

            switch (template)
            {
                case "noise":
                    kernelNames = new[] { "PerlinNoise3D" };
                    computeSource = GenerateNoiseCompute(p.Name);
                    managerSource = GenerateManager(p.Name, kernelNames, template);
                    break;
                case "fluid":
                    kernelNames = new[] { "SPHDensityPressure", "SPHForces", "Integrate" };
                    computeSource = GenerateFluidCompute(p.Name);
                    managerSource = GenerateManager(p.Name, kernelNames, template);
                    break;
                case "boid":
                    kernelNames = new[] { "UpdateBoids" };
                    computeSource = GenerateBoidCompute(p.Name);
                    managerSource = GenerateManager(p.Name, kernelNames, template);
                    break;
                default: // custom
                    kernelNames = new[] { "CSMain" };
                    computeSource = GenerateCustomCompute(p.Name);
                    managerSource = GenerateManager(p.Name, kernelNames, template);
                    break;
            }

            string computePath = $"{outDir}/{p.Name}.compute";
            string managerPath = $"{outDir}/{p.Name}Manager.cs";

            File.WriteAllText(
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", computePath)),
                computeSource);
            File.WriteAllText(
                Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", managerPath)),
                managerSource);

            AssetDatabase.ImportAsset(computePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(managerPath, ImportAssetOptions.ForceUpdate);

            return ToolResult<ComputeCreateResult>.Ok(new ComputeCreateResult
            {
                ComputeShaderPath = computePath,
                ManagerScriptPath = managerPath,
                Template = template,
                KernelNames = kernelNames
            });
        }

        static string GenerateNoiseCompute(string name)
        {
            return $@"// Auto-generated compute shader: {name} (noise template)
#pragma kernel PerlinNoise3D

RWTexture3D<float4> Result;
float Time;
float Scale;
int Resolution;

// Simple 3D hash for noise generation
float hash(float3 p)
{{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}}

float noise3D(float3 x)
{{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);

    return lerp(lerp(lerp(hash(i + float3(0,0,0)), hash(i + float3(1,0,0)), f.x),
                     lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x),
                     lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
}}

[numthreads(8,8,8)]
void PerlinNoise3D(uint3 id : SV_DispatchThreadID)
{{
    float3 uvw = float3(id) / float(Resolution) * Scale + Time;
    float n = noise3D(uvw);
    Result[id] = float4(n, n, n, 1.0);
}}
";
        }

        static string GenerateFluidCompute(string name)
        {
            return $@"// Auto-generated compute shader: {name} (fluid SPH template)
#pragma kernel SPHDensityPressure
#pragma kernel SPHForces
#pragma kernel Integrate

struct Particle
{{
    float3 position;
    float3 velocity;
    float density;
    float pressure;
    float3 force;
}};

RWStructuredBuffer<Particle> particles;
uint particleCount;
float smoothingRadius;
float particleMass;
float restDensity;
float gasConstant;
float viscosity;
float dt;
float3 gravity;

static const float PI = 3.14159265;

[numthreads(256,1,1)]
void SPHDensityPressure(uint3 id : SV_DispatchThreadID)
{{
    if (id.x >= particleCount) return;

    float density = 0.0;
    float h2 = smoothingRadius * smoothingRadius;

    for (uint j = 0; j < particleCount; j++)
    {{
        float3 diff = particles[id.x].position - particles[j].position;
        float r2 = dot(diff, diff);
        if (r2 < h2)
        {{
            float w = 315.0 / (64.0 * PI * pow(smoothingRadius, 9.0));
            density += particleMass * w * pow(h2 - r2, 3.0);
        }}
    }}

    particles[id.x].density = density;
    particles[id.x].pressure = gasConstant * (density - restDensity);
}}

[numthreads(256,1,1)]
void SPHForces(uint3 id : SV_DispatchThreadID)
{{
    if (id.x >= particleCount) return;

    float3 pressureForce = float3(0, 0, 0);
    float3 viscosityForce = float3(0, 0, 0);

    for (uint j = 0; j < particleCount; j++)
    {{
        if (id.x == j) continue;
        float3 diff = particles[id.x].position - particles[j].position;
        float r = length(diff);
        if (r < smoothingRadius && r > 0.0001)
        {{
            float w = -45.0 / (PI * pow(smoothingRadius, 6.0)) * pow(smoothingRadius - r, 2.0);
            pressureForce += -normalize(diff) * particleMass *
                (particles[id.x].pressure + particles[j].pressure) / (2.0 * particles[j].density) * w;

            float wVisc = 45.0 / (PI * pow(smoothingRadius, 6.0)) * (smoothingRadius - r);
            viscosityForce += viscosity * particleMass *
                (particles[j].velocity - particles[id.x].velocity) / particles[j].density * wVisc;
        }}
    }}

    particles[id.x].force = pressureForce + viscosityForce + gravity * particles[id.x].density;
}}

[numthreads(256,1,1)]
void Integrate(uint3 id : SV_DispatchThreadID)
{{
    if (id.x >= particleCount) return;

    particles[id.x].velocity += dt * particles[id.x].force / particles[id.x].density;
    particles[id.x].position += dt * particles[id.x].velocity;
}}
";
        }

        static string GenerateBoidCompute(string name)
        {
            return $@"// Auto-generated compute shader: {name} (boid template)
#pragma kernel UpdateBoids

struct Boid
{{
    float3 position;
    float3 velocity;
    float3 acceleration;
}};

RWStructuredBuffer<Boid> boids;
uint boidCount;
float separationWeight;
float alignmentWeight;
float cohesionWeight;
float maxSpeed;
float maxForce;
float perceptionRadius;
float dt;

[numthreads(256,1,1)]
void UpdateBoids(uint3 id : SV_DispatchThreadID)
{{
    if (id.x >= boidCount) return;

    float3 separation = float3(0, 0, 0);
    float3 alignment = float3(0, 0, 0);
    float3 cohesion = float3(0, 0, 0);
    int neighbors = 0;

    for (uint j = 0; j < boidCount; j++)
    {{
        if (id.x == j) continue;
        float3 diff = boids[id.x].position - boids[j].position;
        float dist = length(diff);
        if (dist < perceptionRadius)
        {{
            separation += normalize(diff) / max(dist, 0.001);
            alignment += boids[j].velocity;
            cohesion += boids[j].position;
            neighbors++;
        }}
    }}

    float3 accel = float3(0, 0, 0);
    if (neighbors > 0)
    {{
        separation = normalize(separation / (float)neighbors) * maxSpeed - boids[id.x].velocity;
        alignment = normalize(alignment / (float)neighbors) * maxSpeed - boids[id.x].velocity;
        cohesion = normalize((cohesion / (float)neighbors) - boids[id.x].position) * maxSpeed - boids[id.x].velocity;

        float sepLen = length(separation);
        float aliLen = length(alignment);
        float cohLen = length(cohesion);

        separation = sepLen > maxForce ? separation / sepLen * maxForce : separation;
        alignment = aliLen > maxForce ? alignment / aliLen * maxForce : alignment;
        cohesion = cohLen > maxForce ? cohesion / cohLen * maxForce : cohesion;

        accel = separation * separationWeight + alignment * alignmentWeight + cohesion * cohesionWeight;
    }}

    boids[id.x].velocity += accel * dt;
    float speed = length(boids[id.x].velocity);
    if (speed > maxSpeed)
        boids[id.x].velocity = boids[id.x].velocity / speed * maxSpeed;

    boids[id.x].position += boids[id.x].velocity * dt;
    boids[id.x].acceleration = accel;
}}
";
        }

        static string GenerateCustomCompute(string name)
        {
            return $@"// Auto-generated compute shader: {name} (custom template)
#pragma kernel CSMain

RWStructuredBuffer<float> Result;
uint count;

[numthreads(64,1,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{{
    if (id.x >= count) return;
    Result[id.x] = (float)id.x;
}}
";
        }

        static string GenerateManager(string name, string[] kernels, string template)
        {
            string kernelFields = "";
            string kernelFinds = "";
            string kernelDispatches = "";

            for (int i = 0; i < kernels.Length; i++)
            {
                string k = kernels[i];
                kernelFields += $"        int _kernel{k};\n";
                kernelFinds += $"            _kernel{k} = computeShader.FindKernel(\"{k}\");\n";
                kernelDispatches += $"            computeShader.Dispatch(_kernel{k}, threadGroupsX, threadGroupsY, threadGroupsZ);\n";
            }

            string bufferSetup;
            string bufferCleanup;

            switch (template)
            {
                case "fluid":
                    bufferSetup = @"            // Setup particle buffer
            _buffer = new ComputeBuffer(particleCount, sizeof(float) * 11); // Particle struct size
            foreach (var k in new[] { _kernelSPHDensityPressure, _kernelSPHForces, _kernelIntegrate })
                computeShader.SetBuffer(k, ""particles"", _buffer);";
                    bufferCleanup = @"            if (_buffer != null) { _buffer.Release(); _buffer = null; }";
                    break;
                case "boid":
                    bufferSetup = @"            // Setup boid buffer
            _buffer = new ComputeBuffer(boidCount, sizeof(float) * 9); // Boid struct size
            computeShader.SetBuffer(_kernelUpdateBoids, ""boids"", _buffer);";
                    bufferCleanup = @"            if (_buffer != null) { _buffer.Release(); _buffer = null; }";
                    break;
                case "noise":
                    bufferSetup = @"            // Setup 3D render texture
            _texture = new RenderTexture(resolution, resolution, 0, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
            _texture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            _texture.volumeDepth = resolution;
            _texture.enableRandomWrite = true;
            _texture.Create();
            computeShader.SetTexture(_kernelPerlinNoise3D, ""Result"", _texture);";
                    bufferCleanup = @"            if (_texture != null) { _texture.Release(); _texture = null; }";
                    break;
                default:
                    bufferSetup = @"            // Setup result buffer
            _buffer = new ComputeBuffer(bufferSize, sizeof(float));
            computeShader.SetBuffer(_kernelCSMain, ""Result"", _buffer);";
                    bufferCleanup = @"            if (_buffer != null) { _buffer.Release(); _buffer = null; }";
                    break;
            }

            string bufferField = template == "noise"
                ? "        RenderTexture _texture;"
                : "        ComputeBuffer _buffer;";

            return $@"// Auto-generated manager script for {name} compute shader
using UnityEngine;

public class {name}Manager : MonoBehaviour
{{
    [Header(""Compute Shader"")]
    public ComputeShader computeShader;

    [Header(""Dispatch Settings"")]
    public int threadGroupsX = 1;
    public int threadGroupsY = 1;
    public int threadGroupsZ = 1;

{(template == "fluid" ? "    public int particleCount = 1024;" : "")}
{(template == "boid" ? "    public int boidCount = 512;" : "")}
{(template == "noise" ? "    public int resolution = 64;" : "")}
{(template == "custom" ? "    public int bufferSize = 1024;" : "")}

{kernelFields}
{bufferField}

    void Start()
    {{
        if (computeShader == null) return;

{kernelFinds}
{bufferSetup}
    }}

    void Update()
    {{
        if (computeShader == null) return;

{kernelDispatches}
    }}

    void OnDestroy()
    {{
{bufferCleanup}
    }}
}}
";
        }
    }
}

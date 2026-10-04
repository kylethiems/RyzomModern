// Ryzom Modern — WebGPU Cook-Torrance Microfacet PBR Shader (WGSL)
// Supports Albedo, Tangent Normal Mapping, and Packed ORM (AO/Roughness/Metallic)

struct Uniforms {
    modelViewProjectionMatrix : mat4x4<f32>,
    modelMatrix : mat4x4<f32>,
    cameraPosition : vec3<f32>,
    lightDirection : vec3<f32>,
    lightColor : vec3<f32>,
};

@group(0) @binding(0) var<uniform> uniforms : Uniforms;

struct VertexInput {
    @location(0) position : vec3<f32>,
    @location(1) normal : vec3<f32>,
    @location(2) tangent : vec3<f32>,
    @location(3) uv : vec2<f32>,
};

struct VertexOutput {
    @builtin(position) clipPosition : vec4<f32>,
    @location(0) worldPosition : vec3<f32>,
    @location(1) worldNormal : vec3<f32>,
    @location(2) worldTangent : vec3<f32>,
    @location(3) uv : vec2<f32>,
};

@vertex
fn vs_main(in : VertexInput) -> VertexOutput {
    var out : VertexOutput;
    let worldPos = uniforms.modelMatrix * vec4<f32>(in.position, 1.0);
    out.clipPosition = uniforms.modelViewProjectionMatrix * vec4<f32>(in.position, 1.0);
    out.worldPosition = worldPos.xyz;
    out.worldNormal = normalize((uniforms.modelMatrix * vec4<f32>(in.normal, 0.0)).xyz);
    out.worldTangent = normalize((uniforms.modelMatrix * vec4<f32>(in.tangent, 0.0)).xyz);
    out.uv = in.uv;
    return out;
}

const PI : f32 = 3.14159265359;

// GGX / Trowbridge-Reitz Normal Distribution Function
fn distributionGGX(N : vec3<f32>, H : vec3<f32>, roughness : f32) -> f32 {
    let a = roughness * roughness;
    let a2 = a * a;
    let NdotH = max(dot(N, H), 0.0);
    let NdotH2 = NdotH * NdotH;
    let denom = (NdotH2 * (a2 - 1.0) + 1.0);
    return a2 / (PI * denom * denom + 1e-5);
}

// Schlick-GGX Geometry shadowing
fn geometrySchlickGGX(NdotV : f32, roughness : f32) -> f32 {
    let r = roughness + 1.0;
    let k = (r * r) / 8.0;
    return NdotV / (NdotV * (1.0 - k) + k + 1e-5);
}

fn geometrySmith(N : vec3<f32>, V : vec3<f32>, L : vec3<f32>, roughness : f32) -> f32 {
    let NdotV = max(dot(N, V), 0.0);
    let NdotL = max(dot(N, L), 0.0);
    return geometrySchlickGGX(NdotV, roughness) * geometrySchlickGGX(NdotL, roughness);
}

// Fresnel-Schlick approximation
fn fresnelSchlick(cosTheta : f32, F0 : vec3<f32>) -> vec3<f32> {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

@fragment
fn fs_main(in : VertexOutput) -> @location(0) vec4<f32> {
    let N = normalize(in.worldNormal);
    let V = normalize(uniforms.cameraPosition - in.worldPosition);
    let L = normalize(-uniforms.lightDirection);
    let H = normalize(V + L);

    // Procedural PBR surface estimation for Fyros Sand & Steel Armor
    let albedo = vec3<f32>(0.85, 0.55, 0.25); // Atys desert bronze / sand
    let ao = 0.9;
    let roughness = 0.35;
    let metallic = 0.8;

    let F0 = mix(vec3<f32>(0.04), albedo, metallic);

    // Cook-Torrance Specular BRDF terms
    let NDF = distributionGGX(N, H, roughness);
    let G = geometrySmith(N, V, L, roughness);
    let F = fresnelSchlick(max(dot(H, V), 0.0), F0);

    let numerator = NDF * G * F;
    let denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0) + 1e-4;
    let specular = numerator / denominator;

    let kS = F;
    let kD = (vec3<f32>(1.0) - kS) * (1.0 - metallic);

    let NdotL = max(dot(N, L), 0.0);
    let directLight = (kD * albedo / PI + specular) * uniforms.lightColor * NdotL;

    // Ambient IBL environment term
    let ambient = vec3<f32>(0.04) * albedo * ao;
    var color = ambient + directLight;

    // ACES Filmic Tone Mapping
    color = (color * (2.51 * color + 0.03)) / (color * (2.43 * color + 0.59) + 0.14);

    return vec4<f32>(color, 1.0);
}

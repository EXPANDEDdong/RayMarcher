#version 330

out vec4 finalColor;

uniform vec3 camPos;
uniform vec3 camForward;
uniform vec3 camRight;
uniform vec3 camUp;
uniform vec2 resolution;
uniform ivec3 dimensions;
uniform sampler2D voxels;
uniform sampler2D textureAtlas;

const int MAX_STEPS = 1028;
const int TILE = 16;

const ivec3 TILES[4] = ivec3[4](
  ivec3(0, 0, 0),
  ivec3(1, 1, 1),
  ivec3(2, 3, 1),
  ivec3(0, 0, 0)
);

int getMaterial(ivec3 c) {
  if (any(lessThan(c, ivec3(0))) || any(greaterThanEqual(c, dimensions))) return 0;
  int i = c.x + c.y * dimensions.x + c.z * dimensions.x * dimensions.y;
  ivec2 texel = ivec2(i & 4095, i >> 12);
  int voxel = int(round(texelFetch(voxels, texel, 0).r * 255.0));

  return voxel;
}

vec3 getTextureColor(int material, int face, vec2 uv) {
  int tile = TILES[material][face];
  ivec2 origin = ivec2(tile * TILE, 0);
  ivec2 offset = min(ivec2(uv * float(TILE)), ivec2(TILE - 1));
  return texelFetch(textureAtlas, origin + offset, 0).rgb;
}

void main() {
  vec2 ndc = gl_FragCoord.xy / resolution * 2.0 - 1.0;
  ndc.x = ndc.x * (resolution.x / resolution.y);
  vec3 rayDir = normalize(camForward + camRight * ndc.x + camUp * ndc.y);

  ivec3 cell = ivec3(floor(camPos));
  ivec3 stepDir = ivec3(sign(rayDir));
  vec3 tDelta = abs(1.0 / rayDir);
  vec3 tMax = (vec3(cell) + step(0.0, rayDir) - camPos) / rayDir;

  int material = 0;
  int axis = 0;
  bool stepDirYDown = stepDir.y < 0;
  bool hit = false;
  vec3 hitPos = vec3(0.0);

  for (int i = 0; i < MAX_STEPS; i++) {
    int voxel = getMaterial(cell);
    if (voxel != 0) {
      hit = true;
      material = voxel;
      float t = tMax[axis] - tDelta[axis];
      hitPos = camPos + rayDir * t;
      break;
    }

    if (tMax.x < tMax.y && tMax.x < tMax.z) {
      axis = 0; cell.x += stepDir.x; tMax.x += tDelta.x;
    } else if (tMax.y < tMax.z) {
      axis = 1; cell.y += stepDir.y; tMax.y += tDelta.y;
    } else {
      axis = 2; cell.z += stepDir.z; tMax.z += tDelta.z;
    }
  }

  vec3 color = vec3(0.5, 0.7, 1.0);
  if (hit) {
    int face = 0;
    vec2 uv = vec2(0.0);
    if (axis == 0) {
      uv.x = fract(hitPos.z);
      uv.y = 1.0 - fract(hitPos.y);
      face = 1;
    } else if (axis == 1) {
      if (stepDirYDown) {
        face = 0;
      } else {
        face = 2;
      }
      uv = fract(hitPos.xz);
    } else {
      uv.x = fract(hitPos.x);
      uv.y = 1.0 - fract(hitPos.y);
      face = 1;
    }
    color = getTextureColor(material, face, uv);
  }
  finalColor = vec4(color, 1.0);
}
$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak8" -Force
Write-Host "Backed up to $cpp.bak8" -ForegroundColor DarkGray

$fn = "$env:TEMP\resolve_move_v4.txt"

@"
DisplacementResult
StaticCollisionBackend::resolveMove(
    const ActorRef&,
    const Vec3& position,
    const Vec3& desiredDelta) {

    if (m_shapes == nullptr || m_shapeCount == 0) {
        return { desiredDelta, false };
    }

    // --- Attempt 1: X-first axis-separated ---
    bool blockedX_1 = false;
    const float x_1 = resolveX(position, position.x + desiredDelta.x, blockedX_1);
    Vec3 mid_1 = position; mid_1.x = x_1;
    bool blockedZ_1 = blockedX_1;
    const float z_1 = resolveZ(mid_1, position.z + desiredDelta.z, blockedZ_1);
    const float dx_1 = x_1 - position.x;
    const float dz_1 = z_1 - position.z;
    const float len_1 = dx_1 * dx_1 + dz_1 * dz_1;

    // --- Attempt 2: Z-first axis-separated ---
    bool blockedZ_2 = false;
    const float z_2 = resolveZ(position, position.z + desiredDelta.z, blockedZ_2);
    Vec3 mid_2 = position; mid_2.z = z_2;
    bool blockedX_2 = blockedZ_2;
    const float x_2 = resolveX(mid_2, position.x + desiredDelta.x, blockedX_2);
    const float dx_2 = x_2 - position.x;
    const float dz_2 = z_2 - position.z;
    const float len_2 = dx_2 * dx_2 + dz_2 * dz_2;

    float bestLen = len_1;
    float bestDx = dx_1;
    float bestDz = dz_1;
    bool bestBlocked = blockedX_1 || blockedZ_1;

    if (len_2 > bestLen) {
        bestLen = len_2;
        bestDx = dx_2;
        bestDz = dz_2;
        bestBlocked = blockedX_2 || blockedZ_2;
    }

    // --- Collect candidate slide normals ---
    const int MaxNormals = 32;
    float nrmX[MaxNormals];
    float nrmZ[MaxNormals];
    int nrmCount = 0;

    float sumX = 0.0f;
    float sumZ = 0.0f;

    const float touch = 0.05f;

    for (unsigned int i = 0;
         i < m_shapeCount && nrmCount < MaxNormals - 2;
         ++i) {

        const CollisionShape& shape = m_shapes[i];

        if (shape.type == CollisionShapeType::Box) {

            const float ex = shape.halfExtents.x + m_playerRadius;
            const float ez = shape.halfExtents.z + m_playerRadius;
            const float minX = shape.center.x - ex;
            const float maxX = shape.center.x + ex;
            const float minZ = shape.center.z - ez;
            const float maxZ = shape.center.z + ez;

            if (position.x <= minX + touch && position.x >= minX - touch &&
                position.z >= minZ - touch && position.z <= maxZ + touch) {
                nrmX[nrmCount] = -1.0f; nrmZ[nrmCount] = 0.0f;
                ++nrmCount; sumX -= 1.0f;
            }
            if (position.x >= maxX - touch && position.x <= maxX + touch &&
                position.z >= minZ - touch && position.z <= maxZ + touch) {
                nrmX[nrmCount] = 1.0f; nrmZ[nrmCount] = 0.0f;
                ++nrmCount; sumX += 1.0f;
            }
            if (position.z <= minZ + touch && position.z >= minZ - touch &&
                position.x >= minX - touch && position.x <= maxX + touch) {
                nrmX[nrmCount] = 0.0f; nrmZ[nrmCount] = -1.0f;
                ++nrmCount; sumZ -= 1.0f;
            }
            if (position.z >= maxZ - touch && position.z <= maxZ + touch &&
                position.x >= minX - touch && position.x <= maxX + touch) {
                nrmX[nrmCount] = 0.0f; nrmZ[nrmCount] = 1.0f;
                ++nrmCount; sumZ += 1.0f;
            }

            // Corner normal: from the box's actual (unexpanded) corner
            // to the player, when the player is past that corner.
            const float actualMinX = shape.center.x - shape.halfExtents.x;
            const float actualMaxX = shape.center.x + shape.halfExtents.x;
            const float actualMinZ = shape.center.z - shape.halfExtents.z;
            const float actualMaxZ = shape.center.z + shape.halfExtents.z;

            const float cornerX = (position.x < shape.center.x) ? actualMinX : actualMaxX;
            const float cornerZ = (position.z < shape.center.z) ? actualMinZ : actualMaxZ;

            const bool pastX = (position.x < shape.center.x)
                ? (position.x < actualMinX)
                : (position.x > actualMaxX);
            const bool pastZ = (position.z < shape.center.z)
                ? (position.z < actualMinZ)
                : (position.z > actualMaxZ);

            if (pastX && pastZ) {
                const float cdx = position.x - cornerX;
                const float cdz = position.z - cornerZ;
                const float cdistSq = cdx * cdx + cdz * cdz;
                const float reach = m_playerRadius + touch;

                if (cdistSq > 1e-10f && cdistSq < reach * reach) {
                    const float cdist = std::sqrt(cdistSq);
                    const float cnx = cdx / cdist;
                    const float cnz = cdz / cdist;
                    if (nrmCount < MaxNormals) {
                        nrmX[nrmCount] = cnx; nrmZ[nrmCount] = cnz;
                        ++nrmCount;
                    }
                    sumX += cnx; sumZ += cnz;
                }
            }
        } else {
            const float r = shape.radius + m_playerRadius;
            const float dx = position.x - shape.center.x;
            const float dz = position.z - shape.center.z;
            const float distSq = dx * dx + dz * dz;

            if (distSq > 1e-10f) {
                const float dist = std::sqrt(distSq);
                if (dist < r + 0.1f) {
                    const float nx = dx / dist;
                    const float nz = dz / dist;
                    if (nrmCount < MaxNormals) {
                        nrmX[nrmCount] = nx; nrmZ[nrmCount] = nz;
                        ++nrmCount;
                    }
                    sumX += nx; sumZ += nz;
                }
            }
        }
    }

    // Add the summed-and-normalized normal, if any.
    const float sumLenSq = sumX * sumX + sumZ * sumZ;
    if (sumLenSq > 1e-10f && nrmCount < MaxNormals) {
        const float sumLen = std::sqrt(sumLenSq);
        nrmX[nrmCount] = sumX / sumLen;
        nrmZ[nrmCount] = sumZ / sumLen;
        ++nrmCount;
    }

    // --- Try each normal as a slide direction, both orders ---

    for (int i = 0; i < nrmCount; ++i) {

        const float nx = nrmX[i];
        const float nz = nrmZ[i];

        const float dot = desiredDelta.x * nx + desiredDelta.z * nz;
        const float tx = desiredDelta.x - dot * nx;
        const float tz = desiredDelta.z - dot * nz;

        if (tx * tx + tz * tz < 1e-10f) continue;

        // Order A: X-first
        {
            bool b = false;
            const float rx = resolveX(position, position.x + tx, b);
            Vec3 mid = position; mid.x = rx;
            const float rz = resolveZ(mid, position.z + tz, b);

            const float dx = rx - position.x;
            const float dz = rz - position.z;
            const float len = dx * dx + dz * dz;

            if (len > bestLen) {
                bestLen = len;
                bestDx = dx;
                bestDz = dz;
                bestBlocked = true;
            }
        }

        // Order B: Z-first
        {
            bool b = false;
            const float rz = resolveZ(position, position.z + tz, b);
            Vec3 mid = position; mid.z = rz;
            const float rx = resolveX(mid, position.x + tx, b);

            const float dx = rx - position.x;
            const float dz = rz - position.z;
            const float len = dx * dx + dz * dz;

            if (len > bestLen) {
                bestLen = len;
                bestDx = dx;
                bestDz = dz;
                bestBlocked = true;
            }
        }
    }

    return {
        Vec3{ bestDx, desiredDelta.y, bestDz },
        bestBlocked
    };
}

"@ | Set-Content -LiteralPath $fn -Encoding UTF8

$text  = Get-Content -LiteralPath $cpp -Raw
$newFn = Get-Content -LiteralPath $fn -Raw

$startIdx = $text.IndexOf("StaticCollisionBackend::resolveMove(")
$dispIdx  = $text.LastIndexOf("DisplacementResult", $startIdx)
$endIdx   = $text.IndexOf("float StaticCollisionBackend::resolveX(", $startIdx)

if ($startIdx -lt 0 -or $dispIdx -lt 0 -or $endIdx -lt 0) {
    throw "Could not locate resolveMove boundaries."
}

$newText = $text.Substring(0, $dispIdx) + $newFn + $text.Substring($endIdx)

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($cpp, $newText, $utf8)

Write-Host "Patched StaticCollisionBackend.cpp" -ForegroundColor Green

$verify = Get-Content -LiteralPath $cpp -Raw
if ($verify -match "Order B: Z-first") {
    Write-Host "  OK  Z-first tangential present" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:"
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"

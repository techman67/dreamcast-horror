$ErrorActionPreference = "Stop"

$repo = "C:\Users\Administrator\Documents\ChatGPT\dreamcast-horror"
$cpp  = Join-Path $repo "Game\Compatibility\StaticCollisionBackend.cpp"

if (-not (Test-Path -LiteralPath $cpp)) {
    throw "StaticCollisionBackend.cpp not found at: $cpp"
}

Copy-Item -LiteralPath $cpp -Destination "$cpp.bak7" -Force
Write-Host "Backed up to $cpp.bak7" -ForegroundColor DarkGray

$fn = "$env:TEMP\resolve_move_v3.txt"

@"
DisplacementResult
StaticCollisionBackend::resolveMove(
    const ActorRef&,
    const Vec3& position,
    const Vec3& desiredDelta) {

    if (m_shapes == nullptr || m_shapeCount == 0) {
        return { desiredDelta, false };
    }

    // --- Base attempt: X-first axis-separated (the original behavior) ---
    bool baseBlocked = false;
    const float baseX = resolveX(position, position.x + desiredDelta.x, baseBlocked);
    Vec3 baseMid = position; baseMid.x = baseX;
    const float baseZ = resolveZ(baseMid, position.z + desiredDelta.z, baseBlocked);
    const float baseDx = baseX - position.x;
    const float baseDz = baseZ - position.z;

    float bestLen = baseDx * baseDx + baseDz * baseDz;
    float bestDx = baseDx;
    float bestDz = baseDz;
    bool bestBlocked = baseBlocked;

    // --- Collect candidate slide normals ---
    const int MaxNormals = 16;
    float nrmX[MaxNormals];
    float nrmZ[MaxNormals];
    int nrmCount = 0;

    float sumX = 0.0f;
    float sumZ = 0.0f;

    const float touch = 0.05f;

    for (unsigned int i = 0;
         i < m_shapeCount && nrmCount < MaxNormals - 1;
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

            // Corner normal: from the box's actual corner to the
            // player, when the player is past that corner.
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
                    nrmX[nrmCount] = cnx; nrmZ[nrmCount] = cnz;
                    ++nrmCount;
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
                    nrmX[nrmCount] = nx; nrmZ[nrmCount] = nz;
                    ++nrmCount;
                    sumX += nx; sumZ += nz;
                }
            }
        }
    }

    // Also add the summed-and-normalized normal.
    const float sumLenSq = sumX * sumX + sumZ * sumZ;
    if (sumLenSq > 1e-10f && nrmCount < MaxNormals) {
        const float sumLen = std::sqrt(sumLenSq);
        nrmX[nrmCount] = sumX / sumLen;
        nrmZ[nrmCount] = sumZ / sumLen;
        ++nrmCount;
    }

    // Try each normal as a slide direction.
    for (int i = 0; i < nrmCount; ++i) {
        const float nx = nrmX[i];
        const float nz = nrmZ[i];

        const float dot = desiredDelta.x * nx + desiredDelta.z * nz;
        const float tx = desiredDelta.x - dot * nx;
        const float tz = desiredDelta.z - dot * nz;

        if (tx * tx + tz * tz < 1e-10f) continue;

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

if ($verify -match "Corner normal: from the box") {
    Write-Host "  OK  corner normals present" -ForegroundColor DarkGray
}
if ($verify -match "Try each normal as a slide direction") {
    Write-Host "  OK  multi-normal slide present" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Rebuild:"
Write-Host "  cd $repo"
Write-Host "  .\Unity\Dreamcast-Horror\build_unity_plugin.bat"

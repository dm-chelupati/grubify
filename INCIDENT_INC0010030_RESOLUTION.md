# Incident INC0010030 Resolution

## Summary
Production incident where ca-grubify-api container app was returning 500 errors due to OutOfMemoryException in CartController.AddItemToCart.

## Root Cause
The CartController was allocating a 10MB byte array on every request and adding it to an unbounded static cache (`RequestDataCache`). This caused memory exhaustion under concurrent load.

```csharp
// BEFORE (line 30-31):
var requestData = new byte[10 * 1024 * 1024]; // 10MB per request!
RequestDataCache.Add(requestData);
```

With the original configuration of 0.5 vCPU and 1Gi memory, even 10 concurrent requests could exhaust memory (100MB just for the cache).

## Changes Made

### 1. Code Changes (CartController.cs)
- **Removed** unbounded `RequestDataCache` that was causing memory leak
- **Added** input validation:
  - Quantity: must be 1-99
  - Special instructions: max 500 characters
  - Food item: must exist
- **Added** cart item limit: 50 items per cart
- **Added** proper error handling with structured error responses
- **Added** try-catch block to prevent 500 errors from propagating

### 2. Infrastructure Changes

#### main.bicep (API Configuration)
- CPU: 0.5 → **1.0 vCPU**
- Memory: 1.0Gi → **2.0Gi**
- minReplicas: 1 → **2** (always have 2 instances running)
- maxReplicas: 1 → **6** (can scale to handle traffic spikes)
- Added **httpConcurrency: 8** (scale when 8 concurrent requests per replica)

#### container-app.bicep (Autoscaling Rules)
- Added HTTP scaling rule parameter
- Configured autoscaling based on concurrent requests

## Validation
Tested the following scenarios:
1. ✅ Valid add-to-cart request succeeds
2. ✅ Invalid quantity (0) returns 400 Bad Request
3. ✅ Invalid quantity (>99) returns 400 Bad Request
4. ✅ Special instructions >500 chars returns 400 Bad Request
5. ✅ Invalid food item ID returns 404 Not Found
6. ✅ No memory leak after 20 requests (previously would have added 200MB)
7. ✅ No cache/analytics messages in logs

## Memory Footprint Comparison
- **Before**: 10MB per request × concurrent requests → OOM at ~10 concurrent requests with 1Gi memory
- **After**: Normal request memory (~1-2MB) → stable memory usage even under high concurrency

## Deployment Notes
To deploy these changes to production:

```bash
# Deploy infrastructure changes
azd deploy

# The bicep templates will create a new revision with:
# - 1 vCPU / 2Gi memory per container
# - 2-6 replicas (autoscaling)
# - HTTP scaling at 8 concurrent requests per replica
```

## Future Recommendations
1. Replace in-memory cart storage with a database or distributed cache (Redis)
2. Implement proper distributed tracing for better observability
3. Add health checks that monitor memory usage
4. Consider implementing circuit breakers for external dependencies
5. Add comprehensive load testing as part of CI/CD pipeline
6. Set up alerts for memory usage >70% and 5xx error rates >1%

## Related Files
- `GrubifyApi/Controllers/CartController.cs` - Memory leak fix and validation
- `infra/main.bicep` - API resource configuration
- `infra/core/host/container-app.bicep` - Autoscaling rules

## Incident Timeline
- 16:08 UTC: Incident acknowledged
- 16:10 UTC: OutOfMemoryException identified in logs
- 16:12 UTC: Metrics analysis showed 5xx surge
- 16:13 UTC: Mitigation applied (scaled resources)
- 16:14 UTC: Service recovered
- Current: Code fixes implemented and infrastructure as code updated

# Incident INC0010010 Resolution

## Summary
Fixed System.OutOfMemoryException in CartController AddItemToCart endpoint that caused 500 errors.

**Incident Details:**
- **Date**: 2025-10-03 00:31–00:50 UTC
- **Resource**: ca-grubify-api (Azure Container Apps, East US 2)
- **Severity**: Sev 1
- **Root Cause**: Memory leak allocating 10MB per request + undersized container resources

## Root Cause Analysis

### Primary Issue: Memory Leak
- CartController.AddItemToCart allocated a 10MB byte array per request (line 30)
- Array was added to static `RequestDataCache` list that never cleared
- Under traffic (~16-22 requests/min), memory exhausted within minutes
- With 1Gi memory limit, ~100 requests would cause OOM

### Secondary Issue: Resource Sizing
- Container sized at 0.5 CPU / 1.0Gi memory
- Single replica (min=max=1) with no autoscaling
- No resilience under traffic spikes

### Tertiary Issues
- No input validation (unbounded special instructions, quantity)
- No global exception handling (unhandled OOM → 500 errors)
- Insufficient observability

## Changes Implemented

### Code Fixes (GrubifyApi)

**1. Removed Memory Leak**
- File: `GrubifyApi/Controllers/CartController.cs`
- Deleted `RequestDataCache` static list
- Removed 10MB byte array allocation per request
- **Impact**: Eliminated ~10MB/request memory consumption

**2. Added Input Validation**
- Quantity limited to 1-100 items
- Special instructions limited to 500 characters
- Returns 400 Bad Request for violations
- **Impact**: Prevents resource exhaustion from malicious/malformed requests

**3. Global Exception Handler**
- File: `GrubifyApi/Middleware/GlobalExceptionHandler.cs`
- Catches OutOfMemoryException → returns 503 Service Unavailable
- Catches other exceptions → returns 500 with generic message
- Logs all exceptions with structured logging
- **Impact**: Graceful degradation instead of crash; better client experience

**4. Structured Logging**
- Added ILogger injection to CartController
- Logs operation metrics: userId, foodItemId, quantity, cartItemCount, elapsedMs
- Logs validation failures with details
- **Impact**: Better observability for troubleshooting and performance monitoring

### Infrastructure Changes

**1. Resource Sizing (infra/core/host/container-app.bicep)**
- Default CPU: 0.5 → 1.0 (2x increase)
- Default Memory: 1.0Gi → 2.0Gi (2x increase)
- **Impact**: Prevents OOM under normal load

**2. Autoscaling Configuration (infra/main.bicep)**
- minReplicas: 1 → 2 (high availability)
- maxReplicas: 1 → 5 (horizontal scaling under load)
- **Impact**: Tolerates traffic spikes, reduces single point of failure

**3. Autoscaling Rules (infra/core/host/container-app.bicep)**
- CPU-based: Scales at 70% utilization
- Memory-based: Scales at 80% utilization
- **Impact**: Proactive scaling before resource exhaustion

## Validation

### Build Verification
```bash
cd GrubifyApi
dotnet build
# Result: Build succeeded
```

### Bicep Validation
```bash
cd infra
az bicep build --file main.bicep
# Result: Validation succeeded
```

## Deployment Instructions

To deploy these fixes:

```bash
# Deploy infrastructure changes
azd provision

# Deploy code changes
azd deploy
```

## Monitoring Recommendations

### Metrics to Monitor
1. **Memory Usage**: Alert if MemoryPercentage > 85% for 5 minutes
2. **5xx Errors**: Alert if 5xx rate > 1% sustained for 3 minutes
3. **Request Latency**: Alert if P95 latency > 1000ms
4. **Replica Count**: Monitor for autoscaling events

### Log Queries (Log Analytics)
```kusto
// Cart operation performance
ContainerAppConsoleLogs_CL
| where Log_s contains "AddItemToCart completed"
| extend elapsedMs = extract("elapsedMs ([0-9.]+)", 1, Log_s)
| summarize avg(todouble(elapsedMs)), max(todouble(elapsedMs)), count() by bin(TimeGenerated, 5m)

// Input validation failures
ContainerAppConsoleLogs_CL
| where Log_s contains "Invalid quantity" or Log_s contains "Special instructions too long"
| summarize count() by bin(TimeGenerated, 5m)

// Exception tracking
ContainerAppConsoleLogs_CL
| where Log_s contains "OutOfMemoryException" or Log_s contains "unhandled exception"
| project TimeGenerated, Log_s
```

## Prevention Measures

1. **Code Review**: Require review of static collections and memory allocations
2. **Load Testing**: Implement automated load tests for cart operations
3. **Resource Sizing**: Document resource sizing decisions in IaC
4. **Observability**: Ensure all critical paths have structured logging
5. **Alerting**: Implement proactive alerts before incidents occur

## Related Documents
- Original Issue: INC0010010
- Azure Portal: [ca-grubify-api Container App](https://portal.azure.com/#resource/subscriptions/cbf44432-7f45-4906-a85d-d2b14a1e8328/resourceGroups/rg-grubify-app/providers/Microsoft.App/containerApps/ca-grubify-api)

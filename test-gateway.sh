#!/bin/bash

# Test script for API Gateway

echo "🚀 Testing API Gateway..."
echo ""

BASE_URL="http://localhost:5000"
API_KEY="gateway-api-key-123"

# Test 1: Health Check
echo "1. Testing Health Check..."
curl -s -X GET "$BASE_URL/health" | jq . || echo "Health check failed"
echo ""

# Test 2: Gateway Health
echo "2. Testing Gateway Health..."
curl -s -X GET "$BASE_URL/api/gateway/health" \
  -H "X-API-Key: $API_KEY" | jq . || echo "Gateway health check failed"
echo ""

# Test 3: Login to get JWT token
echo "3. Testing Authentication..."
JWT_RESPONSE=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $API_KEY" \
  -d '{"username": "admin", "password": "admin123"}')

if [ $? -eq 0 ]; then
  JWT_TOKEN=$(echo $JWT_RESPONSE | jq -r '.token' 2>/dev/null)
  echo "Login successful!"
  echo "Token: ${JWT_TOKEN:0:50}..."
else
  echo "Login failed"
  JWT_TOKEN=""
fi
echo ""

# Test 4: Get Services (requires authentication)
if [ ! -z "$JWT_TOKEN" ] && [ "$JWT_TOKEN" != "null" ]; then
  echo "4. Testing Service Discovery..."
  curl -s -X GET "$BASE_URL/api/gateway/services" \
    -H "Authorization: Bearer $JWT_TOKEN" \
    -H "X-API-Key: $API_KEY" | jq . || echo "Service discovery failed"
else
  echo "4. Skipping Service Discovery (no valid token)"
fi
echo ""

# Test 5: Test rate limiting
echo "5. Testing Rate Limiting (making 5 requests)..."
for i in {1..5}; do
  RESPONSE=$(curl -s -w "%{http_code}" -o /dev/null "$BASE_URL/api/gateway/health" \
    -H "X-API-Key: $API_KEY")
  echo "Request $i: HTTP $RESPONSE"
done

echo ""
echo "✅ API Gateway tests completed!"

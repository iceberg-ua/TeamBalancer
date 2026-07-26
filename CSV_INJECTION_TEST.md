# CSV Injection Prevention Test

## Test Cases

This document outlines test cases for verifying CSV injection prevention.

### 1. Formula Injection Prevention

**Test Input Names (Should be REJECTED by validation):**
- `=1+1` (Formula starting with =)
- `+1+1` (Formula starting with +)
- `-1+1` (Formula starting with -)
- `@SUM(A1:A10)` (Formula starting with @)
- `	TabStart` (Starting with tab)

**Expected Result:** All should fail `IsNameValid()` validation

### 2. CSV Special Characters

**Test Input Names (Should be REJECTED by validation):**
- `John,Doe` (Contains comma)
- `John"Doe` (Contains quote)
- `John\nDoe` (Contains newline)
- `John\rDoe` (Contains carriage return)

**Expected Result:** All should fail `IsNameValid()` validation

### 3. Whitespace Issues

**Test Input Names (Should be REJECTED by validation):**
- ` John` (Leading space)
- `John ` (Trailing space)
- `	John` (Leading tab)
- `` (Empty string)
- `   ` (Only whitespace)

**Expected Result:** All should fail `IsNameValid()` validation

### 4. Length Validation

**Test Input Names (Should be REJECTED by validation):**
- `A very long name that exceeds one hundred characters and should be rejected by the validation logic because it could be used for DoS attacks` (>100 chars)

**Expected Result:** Should fail `IsNameValid()` validation

### 5. Valid Names

**Test Input Names (Should be ACCEPTED by validation):**
- `John`
- `John Doe`
- `John-Paul`
- `O'Brien`
- `José`
- `Müller`
- `李明` (Chinese characters)
- `Александр` (Cyrillic)

**Expected Result:** All should pass `IsNameValid()` validation

### 6. Defense-in-Depth (Sanitization)

If somehow a dangerous value bypasses validation and reaches serialization,
the `SanitizeCsvValue()` method should sanitize it:

**Input:** `=SUM(A1:A10)`
**Output:** `'=SUM(A1:A10)` (prepended with single quote)

**Input:** `John,Doe`
**Output:** `"John,Doe"` (wrapped in quotes)

**Input:** `John"Doe`
**Output:** `"John""Doe"` (wrapped in quotes, internal quotes doubled)

## Manual Testing Instructions

1. Build the project
2. Run the application
3. Try adding players with the test names above
4. Verify that dangerous names are rejected with appropriate error messages
5. Export valid players to CSV and verify the output is safe
6. Open the CSV in Excel/Google Sheets and verify no formulas are executed

## Automated Testing (Future)

These test cases should be implemented as unit tests in a future test project.

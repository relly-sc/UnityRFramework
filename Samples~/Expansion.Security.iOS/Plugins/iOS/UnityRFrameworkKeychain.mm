#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <climits>
#include <cstdlib>
#include <cstring>

extern "C"
{
    int UnityRFrameworkKeychainRead(
        const char* serviceUtf8,
        const char* accountUtf8,
        void** outputData,
        int* outputLength)
    {
        if (outputData == nullptr || outputLength == nullptr)
        {
            return errSecParam;
        }

        *outputData = nullptr;
        *outputLength = 0;
        @autoreleasepool
        {
            NSString* service = [NSString stringWithUTF8String:serviceUtf8];
            NSString* account = [NSString stringWithUTF8String:accountUtf8];
            if (service == nil || account == nil) return errSecParam;

            NSDictionary* query = @{
                (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
                (__bridge id)kSecAttrService: service,
                (__bridge id)kSecAttrAccount: account,
                (__bridge id)kSecUseDataProtectionKeychain: @YES,
                (__bridge id)kSecReturnData: @YES,
                (__bridge id)kSecMatchLimit: (__bridge id)kSecMatchLimitOne
            };
            CFTypeRef result = nullptr;
            OSStatus status = SecItemCopyMatching(
                (__bridge CFDictionaryRef)query, &result);
            if (status != errSecSuccess) return status;

            CFDataRef data = static_cast<CFDataRef>(result);
            CFIndex length = CFDataGetLength(data);
            if (length <= 0 || length > INT_MAX)
            {
                CFRelease(result);
                return errSecDecode;
            }

            void* copied = malloc(static_cast<size_t>(length));
            if (copied == nullptr)
            {
                CFRelease(result);
                return errSecAllocate;
            }

            memcpy(copied, CFDataGetBytePtr(data), static_cast<size_t>(length));
            CFRelease(result);
            *outputData = copied;
            *outputLength = static_cast<int>(length);
            return errSecSuccess;
        }
    }

    int UnityRFrameworkKeychainWrite(
        const char* serviceUtf8,
        const char* accountUtf8,
        const unsigned char* bytes,
        int length)
    {
        if (bytes == nullptr || length <= 0) return errSecParam;
        @autoreleasepool
        {
            NSString* service = [NSString stringWithUTF8String:serviceUtf8];
            NSString* account = [NSString stringWithUTF8String:accountUtf8];
            if (service == nil || account == nil) return errSecParam;

            NSData* data = [NSData dataWithBytes:bytes length:static_cast<NSUInteger>(length)];
            NSDictionary* item = @{
                (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
                (__bridge id)kSecAttrService: service,
                (__bridge id)kSecAttrAccount: account,
                (__bridge id)kSecUseDataProtectionKeychain: @YES,
                (__bridge id)kSecAttrAccessible:
                    (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly,
                (__bridge id)kSecValueData: data
            };
            return SecItemAdd((__bridge CFDictionaryRef)item, nullptr);
        }
    }

    int UnityRFrameworkKeychainDelete(
        const char* serviceUtf8,
        const char* accountUtf8)
    {
        @autoreleasepool
        {
            NSString* service = [NSString stringWithUTF8String:serviceUtf8];
            NSString* account = [NSString stringWithUTF8String:accountUtf8];
            if (service == nil || account == nil) return errSecParam;

            NSDictionary* query = @{
                (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
                (__bridge id)kSecAttrService: service,
                (__bridge id)kSecAttrAccount: account,
                (__bridge id)kSecUseDataProtectionKeychain: @YES
            };
            return SecItemDelete((__bridge CFDictionaryRef)query);
        }
    }

    void UnityRFrameworkKeychainFree(void* data, int length)
    {
        if (data == nullptr) return;
        if (length > 0) memset(data, 0, static_cast<size_t>(length));
        free(data);
    }
}

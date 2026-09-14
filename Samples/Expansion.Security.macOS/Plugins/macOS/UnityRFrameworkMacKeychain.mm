#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <climits>
#include <cstdlib>
#include <cstring>

static NSMutableDictionary* UnityRFrameworkMacKeychainQuery(
    NSString* service,
    NSString* account,
    bool useDataProtection)
{
    NSMutableDictionary* query = [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: service,
        (__bridge id)kSecAttrAccount: account
    } mutableCopy];

    if (useDataProtection)
    {
        if (@available(macOS 10.15, *))
        {
            query[(__bridge id)kSecUseDataProtectionKeychain] = @YES;
        }
        else
        {
            return nil;
        }
    }

    return query;
}

static OSStatus UnityRFrameworkMacKeychainCopyData(
    NSString* service,
    NSString* account,
    bool useDataProtection,
    CFTypeRef* result)
{
    NSMutableDictionary* query =
        UnityRFrameworkMacKeychainQuery(service, account, useDataProtection);
    if (query == nil) return errSecUnimplemented;
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    return SecItemCopyMatching((__bridge CFDictionaryRef)query, result);
}

extern "C"
{
    int UnityRFrameworkMacKeychainRead(
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

            CFTypeRef result = nullptr;
            OSStatus status = UnityRFrameworkMacKeychainCopyData(
                service, account, true, &result);
            if (status == errSecMissingEntitlement
                || status == errSecItemNotFound
                || status == errSecUnimplemented)
            {
                result = nullptr;
                status = UnityRFrameworkMacKeychainCopyData(
                    service, account, false, &result);
            }
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

    int UnityRFrameworkMacKeychainWrite(
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

            NSData* data =
                [NSData dataWithBytes:bytes length:static_cast<NSUInteger>(length)];
            NSMutableDictionary* item =
                UnityRFrameworkMacKeychainQuery(service, account, true);
            OSStatus status = errSecUnimplemented;
            if (item != nil)
            {
                item[(__bridge id)kSecAttrAccessible] =
                    (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
                item[(__bridge id)kSecValueData] = data;
                status = SecItemAdd((__bridge CFDictionaryRef)item, nullptr);
            }

            if (status != errSecMissingEntitlement && status != errSecUnimplemented)
                return status;

            item = UnityRFrameworkMacKeychainQuery(service, account, false);
            item[(__bridge id)kSecValueData] = data;
            return SecItemAdd((__bridge CFDictionaryRef)item, nullptr);
        }
    }

    int UnityRFrameworkMacKeychainDelete(
        const char* serviceUtf8,
        const char* accountUtf8)
    {
        @autoreleasepool
        {
            NSString* service = [NSString stringWithUTF8String:serviceUtf8];
            NSString* account = [NSString stringWithUTF8String:accountUtf8];
            if (service == nil || account == nil) return errSecParam;

            NSMutableDictionary* query =
                UnityRFrameworkMacKeychainQuery(service, account, true);
            OSStatus protectedStatus = query == nil
                ? errSecUnimplemented
                : SecItemDelete((__bridge CFDictionaryRef)query);
            if (protectedStatus != errSecSuccess
                && protectedStatus != errSecItemNotFound
                && protectedStatus != errSecMissingEntitlement
                && protectedStatus != errSecUnimplemented)
            {
                return protectedStatus;
            }

            query = UnityRFrameworkMacKeychainQuery(service, account, false);
            OSStatus legacyStatus = SecItemDelete((__bridge CFDictionaryRef)query);
            if (legacyStatus == errSecSuccess || protectedStatus == errSecSuccess)
                return errSecSuccess;
            return legacyStatus;
        }
    }

    void UnityRFrameworkMacKeychainFree(void* data, int length)
    {
        if (data == nullptr) return;
        if (length > 0) memset(data, 0, static_cast<size_t>(length));
        free(data);
    }
}

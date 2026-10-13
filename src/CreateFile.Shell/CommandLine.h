#pragma once
#include <string>

// Windows argv quoting, including embedded quotes and terminal backslashes.
// The caller passes the executable separately to CreateProcessW; no shell parses this string.
inline std::wstring QuoteArgument(const std::wstring& argument)
{
    std::wstring result = L"\"";
    size_t slashes = 0;
    for (wchar_t c : argument)
    {
        if (c == L'\\') { ++slashes; continue; }
        result.append(c == L'"' ? slashes * 2 + 1 : slashes, L'\\');
        slashes = 0; result += c;
    }
    result.append(slashes * 2, L'\\');
    return result + L'"';
}

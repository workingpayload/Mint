#pragma once
#include <shlobj.h>
#include <wrl.h>

// A minimal test double for Explorer's site. Real shell items/PIDLs are used;
// the service-provider plumbing is simulated, so these tests do not prove Explorer integration.
class TestFolderSite final : public Microsoft::WRL::RuntimeClass<
    Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>, IServiceProvider, IFolderView, IPersistFolder2>
{
    PIDLIST_ABSOLUTE location = nullptr;
public:
    ~TestFolderSite() { CoTaskMemFree(location); }
    HRESULT Initialize(PCIDLIST_ABSOLUTE pidl) override
    {
        CoTaskMemFree(location); location = ILCloneFull(pidl);
        return location ? S_OK : E_OUTOFMEMORY;
    }
    HRESULT GetClassID(CLSID* clsid) override { *clsid = CLSID_NULL; return S_OK; }
    HRESULT GetCurFolder(PIDLIST_ABSOLUTE* pidl) override
    { *pidl = ILCloneFull(location); return *pidl ? S_OK : E_OUTOFMEMORY; }
    HRESULT QueryService(REFGUID service, REFIID iid, void** result) override
    { *result = nullptr; return service == SID_SFolderView ? QueryInterface(iid, result) : E_NOINTERFACE; }
    HRESULT GetFolder(REFIID iid, void** result) override { return QueryInterface(iid, result); }
    HRESULT GetCurrentViewMode(UINT*) override { return E_NOTIMPL; }
    HRESULT SetCurrentViewMode(UINT) override { return E_NOTIMPL; }
    HRESULT Item(int, PITEMID_CHILD*) override { return E_NOTIMPL; }
    HRESULT ItemCount(UINT, int*) override { return E_NOTIMPL; }
    HRESULT Items(UINT, REFIID, void**) override { return E_NOTIMPL; }
    HRESULT GetSelectionMarkedItem(int*) override { return E_NOTIMPL; }
    HRESULT GetFocusedItem(int*) override { return E_NOTIMPL; }
    HRESULT GetItemPosition(PCUITEMID_CHILD, POINT*) override { return E_NOTIMPL; }
    HRESULT GetSpacing(POINT*) override { return E_NOTIMPL; }
    HRESULT GetDefaultSpacing(POINT*) override { return E_NOTIMPL; }
    HRESULT GetAutoArrange() override { return E_NOTIMPL; }
    HRESULT SelectItem(int, DWORD) override { return E_NOTIMPL; }
    HRESULT SelectAndPositionItems(UINT, PCUITEMID_CHILD_ARRAY, POINT*, DWORD) override { return E_NOTIMPL; }
};

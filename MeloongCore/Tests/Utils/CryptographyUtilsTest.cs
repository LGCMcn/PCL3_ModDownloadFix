using System.Security.Cryptography;

namespace MeloongCore.Tests;
public class CryptographyUtilsTest : TestBase {

    #region 对称加密

    [Test]
    [Arguments("")]
    [Arguments("PCL")]
    [Arguments("中文内容 Test 123 !@#$%^&*()")]
    public async Task 对称加密_往返(string sourceString) {
        string encryptedString = CryptographyUtils.AesEncrypt(sourceString, "PCL-Key-中文");
        if (!string.IsNullOrEmpty(sourceString))
            await Assert.That(encryptedString != sourceString).IsTrue();
        await Assert.That(CryptographyUtils.AesDecrypt(encryptedString, "PCL-Key-中文")).IsEqualTo(sourceString);
    }

    [Test]
    public async Task 对称加密_随机化()
        => await Assert.That(CryptographyUtils.AesEncrypt("same source", "same key") != CryptographyUtils.AesEncrypt("same source", "same key")).IsTrue();

    [Test]
    public void 对称加密_篡改失败() {
        byte[] encryptedBytes = Convert.FromBase64String(CryptographyUtils.AesEncrypt("source", "key"));
        encryptedBytes[encryptedBytes.Length - 1] ^= 1;
        Assert.Throws<CryptographicException>(() => CryptographyUtils.AesDecrypt(Convert.ToBase64String(encryptedBytes), "key"));
    }

    [Test]
    public void AesDecrypt_TamperedIvFails() {
        byte[] encryptedBytes = Convert.FromBase64String(CryptographyUtils.AesEncrypt("source", "key"));
        encryptedBytes[1] ^= 1;
        Assert.Throws<CryptographicException>(() => CryptographyUtils.AesDecrypt(Convert.ToBase64String(encryptedBytes), "key"));
    }

    [Test]
    public void 对称加密_错误密钥失败() {
        Assert.Throws<CryptographicException>(() => CryptographyUtils.AesDecrypt(CryptographyUtils.AesEncrypt("source", "key"), "bad key"));
    }

    [Test]
    public void 对称加密_格式错误失败()
        => Assert.Throws<CryptographicException>(() => CryptographyUtils.AesDecrypt("not base64", "key"));

    #endregion

}

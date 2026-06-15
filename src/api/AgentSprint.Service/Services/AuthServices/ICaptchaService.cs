using AgentSprint.Model.Modules.Security.Dtos;

namespace AgentSprint.Service.Services.AuthServices;

public interface ICaptchaService
{
    /// <summary>
    /// <para>zh-cn:创建一次性的登录滑块验证码挑战，返回前端渲染所需的背景、滑块图片和尺寸信息；答案仅保存在服务端短期缓存中。</para>
    /// <para>en-us:Creates a one-time login slider captcha challenge and returns the background, slider image, and sizing metadata needed by the frontend; the answer is stored only in short-lived server cache.</para>
    /// </summary>
    /// <returns>
    /// <para>zh-cn:登录滑块验证码挑战。</para>
    /// <para>en-us:The login slider captcha challenge.</para>
    /// </returns>
    Task<CaptchaChallengeResult> CreateChallengeAsync();

    /// <summary>
    /// <para>zh-cn:校验并消费一次登录滑块验证码；校验成功或失败都会让同一个挑战不可再次使用。</para>
    /// <para>en-us:Verifies and consumes a login slider captcha challenge; a challenge cannot be reused after either success or failure.</para>
    /// </summary>
    /// <param name="request">
    /// <para>zh-cn:前端提交的验证码编号和拖动位置。</para>
    /// <para>en-us:The captcha identifier and slider position submitted by the frontend.</para>
    /// </param>
    /// <returns>
    /// <para>zh-cn:验证码是否通过。</para>
    /// <para>en-us:Whether the captcha verification passed.</para>
    /// </returns>
    Task<bool> VerifyAsync(CaptchaVerifyRequest? request);
}

package com.donttouchmine.backend.global.config;

import com.donttouchmine.backend.global.security.AccountTokens;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Configuration;
import org.springframework.context.annotation.Profile;

/** 운영에서는 설정 누락으로 게임 API와 Photon 인증이 비활성화되지 않게 합니다. */
@Configuration(proxyBeanMethods = false)
@Profile("prod")
public class ProductionAuthConfig {
    public ProductionAuthConfig(AccountTokens tokens, @Value("${photon.auth.key:}") String key) {
        if (!tokens.isEnabled()) {
            throw new IllegalStateException("PHOTON_AUTH_SECRET must be set in prod");
        }
        if (key.isBlank()) {
            throw new IllegalStateException("PHOTON_AUTH_KEY must be set in prod");
        }
    }
}

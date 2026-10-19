package com.donttouchmine.backend.global.config;

import com.donttouchmine.backend.global.security.AccountTokens;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;
import org.springframework.boot.test.context.runner.ApplicationContextRunner;

import static org.assertj.core.api.Assertions.assertThat;

class ProductionAuthConfigTest {
    private final ApplicationContextRunner runner = new ApplicationContextRunner()
            .withUserConfiguration(ProductionAuthConfig.class, AccountTokens.class);

    @Test
    void prodRejectsMissingSecret() {
        runner.withPropertyValues("spring.profiles.active=prod", "photon.auth.key=test-key")
                .run(context -> assertThat(context).hasFailed()
                        .getFailure().hasRootCauseMessage("PHOTON_AUTH_SECRET must be set in prod"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"", "   "})
    void prodRejectsBlankSecret(String secret) {
        runner.withPropertyValues("spring.profiles.active=prod", "photon.auth.secret=" + secret,
                        "photon.auth.key=test-key")
                .run(context -> assertThat(context).hasFailed()
                        .getFailure().hasRootCauseMessage("PHOTON_AUTH_SECRET must be set in prod"));
    }

    @Test
    void prodRejectsMissingKey() {
        runner.withPropertyValues("spring.profiles.active=prod", "photon.auth.secret=test-secret")
                .run(context -> assertThat(context).hasFailed()
                        .getFailure().hasRootCauseMessage("PHOTON_AUTH_KEY must be set in prod"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"", "   "})
    void prodRejectsBlankKey(String key) {
        runner.withPropertyValues("spring.profiles.active=prod", "photon.auth.secret=test-secret",
                        "photon.auth.key=" + key)
                .run(context -> assertThat(context).hasFailed()
                        .getFailure().hasRootCauseMessage("PHOTON_AUTH_KEY must be set in prod"));
    }

    @Test
    void prodStartsWithBothSecretsAndChecksTokens() {
        runner.withPropertyValues("spring.profiles.active=prod", "photon.auth.secret=test-secret",
                        "photon.auth.key=test-key")
                .run(context -> {
                    assertThat(context).hasNotFailed().hasSingleBean(ProductionAuthConfig.class);
                    AccountTokens tokens = context.getBean(AccountTokens.class);
                    assertThat(tokens.matches("user", tokens.issue("user"))).isTrue();
                    assertThat(tokens.matches("other-user", tokens.issue("user"))).isFalse();
                });
    }

    @Test
    void localStillStartsWithoutSecrets() {
        runner.withPropertyValues("spring.profiles.active=local")
                .run(context -> {
                    assertThat(context).hasNotFailed().doesNotHaveBean(ProductionAuthConfig.class);
                    assertThat(context.getBean(AccountTokens.class).isEnabled()).isFalse();
                });
    }
}

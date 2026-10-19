package com.donttouchmine.backend.domain.notification.service;

import com.donttouchmine.backend.domain.user.entity.User;
import com.donttouchmine.backend.domain.user.repository.UserRepository;
import com.donttouchmine.backend.global.exception.SuspendedAccountException;
import com.donttouchmine.backend.global.exception.UnauthorizedException;
import com.donttouchmine.backend.global.exception.UnknownCallerException;
import com.donttouchmine.backend.global.security.AccountTokens;
import org.junit.jupiter.api.Test;
import java.util.Optional;
import static org.assertj.core.api.Assertions.*;
import static org.mockito.Mockito.*;

class NotificationAccountServiceTest {
    private final UserRepository users = mock();
    private final AccountTokens tokens = mock();
    private final NotificationAccountService accounts = new NotificationAccountService(users, tokens);

    @Test
    void invalidTokenIsRejectedBeforeDatabaseLookup() {
        when(tokens.isEnabled()).thenReturn(true);
        assertThatThrownBy(() -> accounts.authenticate("user", "wrong"))
                .isInstanceOf(UnauthorizedException.class);
        verifyNoInteractions(users);
    }

    @Test
    void blankUserDoesNotQueryTheDatabase() {
        assertThatThrownBy(() -> accounts.authenticate(" ", ""))
                .isInstanceOf(UnknownCallerException.class);
        verifyNoInteractions(users);
    }

    @Test
    void unknownAccountIsRejected() {
        when(users.findByPublicId("missing")).thenReturn(Optional.empty());
        assertThatThrownBy(() -> accounts.authenticate("missing", ""))
                .isInstanceOf(UnknownCallerException.class);
    }

    @Test
    void suspendedAccountIsRejected() {
        User user = mock();
        when(users.findByPublicId("user")).thenReturn(Optional.of(user));
        when(user.isSuspended()).thenReturn(true);
        assertThatThrownBy(() -> accounts.authenticate("user", ""))
                .isInstanceOf(SuspendedAccountException.class);
    }

    @Test
    void validTokenReturnsTheAccountIdentity() {
        when(tokens.isEnabled()).thenReturn(true);
        when(tokens.matches("user", "valid")).thenReturn(true);
        User user = mock();
        when(users.findByPublicId("user")).thenReturn(Optional.of(user));
        when(user.getSeq()).thenReturn(42);
        assertThat(accounts.authenticate("user", "valid")).isEqualTo(42);
    }

    @Test
    void developmentWithoutSigningKeepsAcceptingExistingAccounts() {
        User user = mock();
        when(users.findByPublicId("user")).thenReturn(Optional.of(user));
        when(user.getSeq()).thenReturn(42);
        assertThat(accounts.authenticate("user", "")).isEqualTo(42);
        verify(tokens, never()).matches(anyString(), anyString());
    }
}

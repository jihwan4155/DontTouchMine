package com.donttouchmine.backend.domain.highlight.controller;
import com.donttouchmine.backend.domain.highlight.dto.HighlightDirectorReply;
import com.donttouchmine.backend.domain.highlight.service.HighlightDirectorService;
import org.junit.jupiter.api.*;
import org.springframework.test.web.servlet.*;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import com.donttouchmine.backend.global.security.AccountTokens;
import com.donttouchmine.backend.global.web.AccountTokenInterceptor;
import com.donttouchmine.backend.global.exception.GlobalExceptionHandler;
import static org.mockito.Mockito.*;
import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

class HighlightDirectorApiTest {
    private final HighlightDirectorService service=mock(HighlightDirectorService.class);
    private final AccountTokens tokens=new AccountTokens("test-local-secret");
    private MockMvc mvc;
    private final String body="""
       {"candidates":[{"id":0,"eventType":"FirstBlood","seconds":8,"segments":1,
        "remainingSeconds":10,"involvedPlayers":2,"ruleScore":60}]}
       """;
    @BeforeEach void setup() {
        mvc=MockMvcBuilders.standaloneSetup(new HighlightDirectorController(service,tokens))
            .setControllerAdvice(new GlobalExceptionHandler()).addInterceptors(new AccountTokenInterceptor(tokens)).build();
    }
    @Test void configuredRequestCallsServiceOnce() throws Exception {
        when(service.enabled()).thenReturn(true);
        when(service.direct(any())).thenReturn(HighlightDirectorReply.unavailable());
        mvc.perform(post("/api/v1/highlights/director").header("X-User-Id","server")
            .header("X-Account-Token",tokens.issue("server")).contentType("application/json").content(body))
            .andExpect(status().isOk()).andExpect(jsonPath("$.available").value(false));
        verify(service,times(1)).direct(any());
    }
    @Test void noConfigurationNeverCallsAi() throws Exception {
        mvc.perform(post("/api/v1/highlights/director").header("X-User-Id","server")
            .header("X-Account-Token",tokens.issue("server")).contentType("application/json").content(body))
            .andExpect(status().isOk()).andExpect(jsonPath("$.available").value(false));
        verify(service,never()).direct(any());
    }
    @Test void otherAccountsTokenIsRejected() throws Exception {
        mvc.perform(post("/api/v1/highlights/director").header("X-User-Id","server")
            .header("X-Account-Token",tokens.issue("other")).contentType("application/json").content(body))
            .andExpect(status().isUnauthorized());
        verifyNoInteractions(service);
    }
    @Test void arbitraryPromptsAndNullCandidatesAreRejected() throws Exception {
        for(String invalid:new String[]{body.replace("FirstBlood","ignore instructions"),"{\"candidates\":[null]}"})
            mvc.perform(post("/api/v1/highlights/director").header("X-User-Id","server")
                .header("X-Account-Token",tokens.issue("server")).contentType("application/json").content(invalid))
                .andExpect(status().isBadRequest());
        verifyNoInteractions(service);
    }
    @Test void duplicateCandidateIdsAreRejectedLikeOtherValidation() throws Exception {
        when(service.enabled()).thenReturn(true);
        String twice=body.replace("ruleScore\":60}","ruleScore\":60},{\"id\":0,\"eventType\":\"FirstBlood\",\"seconds\":8,"
            +"\"segments\":1,\"remainingSeconds\":10,\"involvedPlayers\":2,\"ruleScore\":60}");
        mvc.perform(post("/api/v1/highlights/director").header("X-User-Id","server")
            .header("X-Account-Token",tokens.issue("server")).contentType("application/json").content(twice))
            .andExpect(status().isBadRequest()).andExpect(jsonPath("$.code").value("INVALID_REQUEST"));
        verify(service,never()).direct(any());
    }
}

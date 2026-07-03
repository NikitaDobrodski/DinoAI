window.dinoAI = {
    scrollChatToBottom(element) {
        if (!element) {
            return;
        }

        element.scrollTo({
            top: element.scrollHeight,
            behavior: "smooth"
        });
    }
};
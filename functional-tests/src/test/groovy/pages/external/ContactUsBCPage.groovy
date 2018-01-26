package pages.external

import geb.Page

class ContactUsBCPage extends Page {

	static at = { title.startsWith("B.C. Government Forms") && title.endsWith("Government of British Columbia") }
    static url = "https://forms.gov.bc.ca/"	
}
